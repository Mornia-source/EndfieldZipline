using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using EndfieldZipline.Content.Tiles;
using EndfieldZipline.Content.Items;
using EndfieldZipline.Content.Players;
using EndfieldZipline.Common.Configs;

namespace EndfieldZipline.Content.Systems
{
    public class ZiplinePlacementOverlaySystem : ModSystem
    {
        private static Asset<Texture2D> _ziplineIcon;
        private static Asset<Texture2D> _ziplineIconError;
        private static Asset<Texture2D> _arrowIcon;
		private static Asset<Texture2D> _nextTargetRing;
		private static Asset<Texture2D> _nextTargetZipline;
		private static Asset<Texture2D> _ziplineConfigIconOff;
		private static Asset<Texture2D> _ziplineConfigIconOffHover;
		private static Asset<Texture2D> _ziplineConfigIconOn;
		private static Asset<Texture2D> _ziplineConfigIconOnHover;
		public static bool ZiplineConfigSelected;
		private static int _equipSlotsVerticalAlignment;
		private static bool _accSlotsHooked;
		private static bool _itemSlotHooked;
		private static bool _allowEquipContextInThisCall;
		private static FieldInfo _mainMHField;
		private static int _lastEquipPage;
		private static bool _ziplineConfigJustEnabled;
		private static int _ziplineConfigGraceFrames;
		private const float ZiplineSlotScaleMultiplier = 1.4f;

		private static Point16? _lastNextTarget;
		private static int _nextTargetPopTimer;
		private const int NextTargetPopDuration = 18;
		private const float NextTargetPopStartScale = 1.6f;
		private static bool _lastWasRiding;
		private static bool _suppressNextTargetSound;

        private const int SearchRangeTiles = 100;
        private const int MaxIndicatorsPerSide = 3;
        private const float ArrowScale = 0.55f;
        private const float ArrowRingRadiusMultiplier = 1.15f;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            _ziplineIcon = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Icon/滑索缩略", AssetRequestMode.ImmediateLoad);
            _ziplineIconError = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Icon/红温滑索架", AssetRequestMode.ImmediateLoad);
            _arrowIcon = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Icon/指标", AssetRequestMode.ImmediateLoad);
			_nextTargetRing = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Icon/环儿", AssetRequestMode.ImmediateLoad);
			_nextTargetZipline = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Icon/滑索16x", AssetRequestMode.ImmediateLoad);
			_ziplineConfigIconOff = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Item/滑索配置_未选中", AssetRequestMode.ImmediateLoad);
			_ziplineConfigIconOffHover = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Item/滑索配置_未选中_hover", AssetRequestMode.ImmediateLoad);
			_ziplineConfigIconOn = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Item/滑索配置_选中", AssetRequestMode.ImmediateLoad);
			_ziplineConfigIconOnHover = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Item/滑索配置_选中_hover", AssetRequestMode.ImmediateLoad);
			ZiplineConfigSelected = false;
			_equipSlotsVerticalAlignment = 0;
			_lastEquipPage = Main.EquipPage;
			_ziplineConfigJustEnabled = false;
			_ziplineConfigGraceFrames = 0;
			TryHookAccessorySlotDrawing();
			TryHookItemSlotDrawing();
        }

        public override void Unload()
        {
            _ziplineIcon = null;
            _ziplineIconError = null;
            _arrowIcon = null;
			_nextTargetRing = null;
			_nextTargetZipline = null;
			_ziplineConfigIconOff = null;
			_ziplineConfigIconOffHover = null;
			_ziplineConfigIconOn = null;
			_ziplineConfigIconOnHover = null;
			_lastNextTarget = null;
			_nextTargetPopTimer = 0;
			_lastWasRiding = false;
			_suppressNextTargetSound = false;
			ZiplineConfigSelected = false;
			_equipSlotsVerticalAlignment = 0;
			_accSlotsHooked = false;
			_itemSlotHooked = false;
			_allowEquipContextInThisCall = false;
			_lastEquipPage = 0;
			_ziplineConfigJustEnabled = false;
			_ziplineConfigGraceFrames = 0;
        }

        private static void TryHookAccessorySlotDrawing()
        {
            if (_accSlotsHooked)
                return;

            MethodInfo drawAccSlots = typeof(Terraria.ModLoader.AccessorySlotLoader).GetMethod(
                "DrawAccSlots",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(int) },
                modifiers: null
            );

            if (drawAccSlots is null)
                return;

            Terraria.ModLoader.MonoModHooks.Add(drawAccSlots,
                (Action<Action<Terraria.ModLoader.AccessorySlotLoader, int>, Terraria.ModLoader.AccessorySlotLoader, int>)AccessorySlotLoader_DrawAccSlots);
            _accSlotsHooked = true;
        }

        private static void AccessorySlotLoader_DrawAccSlots(Action<Terraria.ModLoader.AccessorySlotLoader, int> orig, Terraria.ModLoader.AccessorySlotLoader self, int num20)
        {
            _equipSlotsVerticalAlignment = num20;
            if (ZiplineConfigSelected && Main.playerInventory)
                return;

            orig(self, num20);
        }

        private static void TryHookItemSlotDrawing()
        {
            if (_itemSlotHooked)
                return;

            MethodInfo draw = typeof(Terraria.UI.ItemSlot).GetMethod(
                "Draw",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(SpriteBatch), typeof(Terraria.Item[]), typeof(int), typeof(int), typeof(Vector2), typeof(Color) },
                modifiers: null
            );

            MethodInfo handle = typeof(Terraria.UI.ItemSlot).GetMethod(
                "Handle",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Terraria.Item[]), typeof(int), typeof(int) },
                modifiers: null
            );

            if (draw is null || handle is null)
                return;

            Terraria.ModLoader.MonoModHooks.Add(draw,
                (Action<Action<SpriteBatch, Terraria.Item[], int, int, Vector2, Color>, SpriteBatch, Terraria.Item[], int, int, Vector2, Color>)ItemSlot_Draw_Detour);
            Terraria.ModLoader.MonoModHooks.Add(handle,
                (Action<Action<Terraria.Item[], int, int>, Terraria.Item[], int, int>)ItemSlot_Handle_Detour);
            _itemSlotHooked = true;
        }

        private static int GetMainMH()
        {
            try
            {
                _mainMHField ??= typeof(Main).GetField("mH", BindingFlags.NonPublic | BindingFlags.Static);
                if (_mainMHField != null)
                    return (int)_mainMHField.GetValue(null);
            }
            catch
            {
            }
            return 0;
        }

        private static bool ShouldHideEquipContext(int context)
        {
            if (!ZiplineConfigSelected || !Main.playerInventory)
                return false;
            if (_allowEquipContextInThisCall)
                return false;

            int abs = Math.Abs(context);
            return abs == Terraria.UI.ItemSlot.Context.EquipArmor
                || abs == Terraria.UI.ItemSlot.Context.EquipArmorVanity
                || abs == Terraria.UI.ItemSlot.Context.EquipAccessory
                || abs == Terraria.UI.ItemSlot.Context.EquipAccessoryVanity
                || abs == Terraria.UI.ItemSlot.Context.EquipDye
                || abs == Terraria.UI.ItemSlot.Context.EquipGrapple
                || abs == Terraria.UI.ItemSlot.Context.EquipMount
                || abs == Terraria.UI.ItemSlot.Context.EquipMinecart
                || abs == Terraria.UI.ItemSlot.Context.EquipPet
                || abs == Terraria.UI.ItemSlot.Context.EquipLight
                || abs == Terraria.UI.ItemSlot.Context.ModdedAccessorySlot
                || abs == Terraria.UI.ItemSlot.Context.ModdedVanityAccessorySlot
                || abs == Terraria.UI.ItemSlot.Context.ModdedDyeSlot;
        }

        private static void ItemSlot_Draw_Detour(Action<SpriteBatch, Terraria.Item[], int, int, Vector2, Color> orig, SpriteBatch spriteBatch, Terraria.Item[] inv, int context, int slot, Vector2 position, Color lightColor)
        {
            if (ShouldHideEquipContext(context))
                return;

            orig(spriteBatch, inv, context, slot, position, lightColor);
        }

        private static void ItemSlot_Handle_Detour(Action<Terraria.Item[], int, int> orig, Terraria.Item[] inv, int context, int slot)
        {
            if (ShouldHideEquipContext(context))
                return;

            orig(inv, context, slot);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text", StringComparison.Ordinal));
            if (mouseTextIndex == -1)
                return;

            layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                "EndfieldZipline: Zipline Config Button",
                DrawZiplineConfigButtonLayer,
                InterfaceScaleType.UI
            ));
        }

        private bool DrawZiplineConfigButtonLayer()
        {
            if (Main.gameMenu)
                return true;
            if (!Main.playerInventory)
            {
                ZiplineConfigSelected = false;
                return true;
            }

            EndfieldZiplineConfig config = ModContent.GetInstance<EndfieldZiplineConfig>();
            if (config is not null && config.SimpleModeEnabled)
            {
                ZiplineConfigSelected = false;
                _ziplineConfigJustEnabled = false;
                _ziplineConfigGraceFrames = 0;
                return true;
            }

            // Late check (after vanilla tab click processing): switching to any other page should close zipline config.
            if (ZiplineConfigSelected && Main.EquipPage != 0)
            {
                ZiplineConfigSelected = false;
                _ziplineConfigJustEnabled = false;
                _ziplineConfigGraceFrames = 0;
            }

            DrawZiplineConfigToggleButton();
            if (ZiplineConfigSelected)
                DrawZiplineConfigSlots();
            return true;
        }

        private static void DrawZiplineConfigSlots()
        {
            Player player = Main.LocalPlayer;
            if (player is null || !player.active)
                return;
            ZiplinePlayer ziplinePlayer = player.GetModPlayer<ZiplinePlayer>();
            if (ziplinePlayer is null)
                return;

            float scale = Main.inventoryScale * ZiplineSlotScaleMultiplier;
            int baseX = Main.screenWidth - 64 - 28;
            int baseY = 174 + GetMainMH();
            int slotY = (int)(baseY + (3 * 56) * scale + 4 - (172 * scale));
            int slotSize = (int)(TextureAssets.InventoryBack.Value.Width * scale);
            int spacing = (int)(56 * scale);
            Rectangle leftRect = new Rectangle(baseX - spacing, slotY, slotSize, slotSize);
            Rectangle rightRect = new Rectangle(baseX, slotY, slotSize, slotSize);

            Rectangle coverRect = new Rectangle(baseX - spacing * 2, slotY, spacing * 3 + slotSize, (int)(10 * 56 * scale));
            if (coverRect.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface)
            {
                player.mouseInterface = true;
                Main.blockMouse = true;
            }

            DrawCustomItemSlot(ref ziplinePlayer.FuelSlotItem, leftRect, ItemSlot.Context.EquipDye, ItemSlot.Context.InventoryItem, IsVanillaDyeItem, Language.GetTextValue("Mods.EndfieldZipline.UI.DyeSlot"));
            DrawCustomItemSlot(ref ziplinePlayer.ZiplineFrameSlotItem, rightRect, ItemSlot.Context.EquipAccessory, ItemSlot.Context.InventoryItem, IsZiplineFrameItem, Language.GetTextValue("Mods.EndfieldZipline.UI.ZiplineFrameSlot"));
        }

        private static bool IsVanillaDyeItem(Item item)
        {
            if (item == null || item.IsAir)
                return true;
            return item.dye > 0 && item.ModItem == null;
        }

        private static bool IsZiplineFrameItem(Item item)
        {
            if (item == null || item.IsAir)
                return true;
            int type = item.type;
            return type == ModContent.ItemType<CommonZiplineFrame>()
                || type == ModContent.ItemType<AdamantiteZiplineFrame>()
                || type == ModContent.ItemType<TitaniumZiplineFrame>()
                || type == ModContent.ItemType<MechanicalZiplineFrame>()
                || type == ModContent.ItemType<NormalZiplineFrame>()
                || type == ModContent.ItemType<BoulderZiplineFrame>()
                || type == ModContent.ItemType<NightglowZiplineFrame>();
        }

        private static bool Isziplineframeltem(Item item) => IsZiplineFrameItem(item);
        private static bool lsZiplineframeltem(Item item) => IsZiplineFrameItem(item);

        private static void DrawCustomItemSlot(ref Item item, Rectangle rect, int drawContext, int handleContext, Func<Item, bool> validItemFunc, string hoverText)
        {
            if (item == null)
            {
                item = new Item();
                item.SetDefaults(0);
            }

            bool prevAllow = _allowEquipContextInThisCall;
            _allowEquipContextInThisCall = true;
            float prevScale = Main.inventoryScale;
            Main.inventoryScale = prevScale * ZiplineSlotScaleMultiplier;
            try
            {
                if (rect.Contains(Main.mouseX, Main.mouseY) && !PlayerInput.IgnoreMouseInterface)
                {
                    Main.LocalPlayer.mouseInterface = true;
                    Main.blockMouse = true;
                    if (validItemFunc == null || validItemFunc(Main.mouseItem))
                        ItemSlot.Handle(ref item, handleContext);
                    Main.instance.MouseText(hoverText);
                }
                ItemSlot.Draw(Main.spriteBatch, ref item, drawContext, rect.TopLeft());
            }
            finally
            {
                Main.inventoryScale = prevScale;
                _allowEquipContextInThisCall = prevAllow;
            }
        }

        private static void DrawZiplineConfigToggleButton()
        {
            const float size = 32f;
            Vector2 pos = new Vector2(Main.screenWidth - 82f, 95f);
            Rectangle hitbox = new Rectangle((int)pos.X, (int)pos.Y, (int)size, (int)size);

            bool hovering = hitbox.Contains(Main.mouseX, Main.mouseY);
            if (hovering)
            {
                Main.LocalPlayer.mouseInterface = true;
                Main.blockMouse = true;
                Main.instance.MouseText(Language.GetTextValue("Mods.EndfieldZipline.UI.ZiplineConfigButton"));
                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    bool next = !ZiplineConfigSelected;
                    ZiplineConfigSelected = next;
                    if (next)
                    {
                        _ziplineConfigJustEnabled = true;
                        _ziplineConfigGraceFrames = 2;
                        Main.EquipPage = 0;
                    }
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }

            Asset<Texture2D> icon;
            if (!ZiplineConfigSelected)
                icon = hovering ? _ziplineConfigIconOffHover : _ziplineConfigIconOff;
            else
                icon = hovering ? _ziplineConfigIconOnHover : _ziplineConfigIconOn;

            if (icon != null)
                Main.spriteBatch.Draw(icon.Value, hitbox, Color.White);
        }

        public override void PostUpdateInput()
        {
            if (!Main.playerInventory)
            {
                ZiplineConfigSelected = false;
                _lastEquipPage = Main.EquipPage;
                _ziplineConfigJustEnabled = false;
                _ziplineConfigGraceFrames = 0;
                return;
            }

            if (ZiplineConfigSelected)
            {
                int currentEquipPage = Main.EquipPage;
                if (_ziplineConfigJustEnabled)
                {
                    Main.EquipPage = 0;
                    _ziplineConfigJustEnabled = false;
                }

                if (_ziplineConfigGraceFrames > 0)
                    _ziplineConfigGraceFrames--;
                else
                {
                    // Player switched to housing/camera pages -> close zipline config and allow vanilla UI.
                    if (currentEquipPage != 0)
                    {
                        ZiplineConfigSelected = false;
                        _ziplineConfigJustEnabled = false;
                        _ziplineConfigGraceFrames = 0;
                    }
                }
            }

            _lastEquipPage = Main.EquipPage;
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu)
                return;

            Player player = Main.LocalPlayer;
            if (player is null || !player.active)
                return;

            ZiplinePlayer ziplinePlayer = player.GetModPlayer<ZiplinePlayer>();
            if (ziplinePlayer != null && ziplinePlayer.IsOnZipline)
            {
                DrawNextZiplineTargetHighlight(spriteBatch, player, ziplinePlayer);
            }

            if (player.HeldItem is null || player.HeldItem.IsAir)
                return;

            if (player.HeldItem.type != ModContent.ItemType<LongDistanceZipline>())
                return;

            DrawForDirection(spriteBatch, player, -1);
            DrawForDirection(spriteBatch, player, 1);
        }

        private static void DrawNextZiplineTargetHighlight(SpriteBatch spriteBatch, Player player, ZiplinePlayer ziplinePlayer)
        {
            Texture2D ring = _nextTargetRing?.Value;
            Texture2D zipline = _nextTargetZipline?.Value;
            Texture2D arrow = _arrowIcon?.Value;
            if (ring is null || zipline is null || arrow is null)
                return;

            EndfieldZiplineConfig config = ModContent.GetInstance<EndfieldZiplineConfig>();

			if (_lastWasRiding && !ziplinePlayer.IsRiding)
				_suppressNextTargetSound = true;
			_lastWasRiding = ziplinePlayer.IsRiding;

			Point16? targetZipline = ziplinePlayer.GetNextZiplineTarget(player.direction);
			if (!targetZipline.HasValue)
			{
				_lastNextTarget = null;
				_nextTargetPopTimer = 0;
				return;
			}

			if (!_lastNextTarget.HasValue || _lastNextTarget.Value.X != targetZipline.Value.X || _lastNextTarget.Value.Y != targetZipline.Value.Y)
			{
				_lastNextTarget = targetZipline;
				_nextTargetPopTimer = NextTargetPopDuration;
				if (!_suppressNextTargetSound && !(config is not null && config.DisableZiplineSounds))
					SoundEngine.PlaySound(new SoundStyle("EndfieldZipline/Content/Sounds/选择目的地"), player.Center);
				_suppressNextTargetSound = false;
			}

			float popScale = 1f;
			if (_nextTargetPopTimer > 0)
			{
				float t = 1f - (_nextTargetPopTimer / (float)NextTargetPopDuration);
				// Ease-out shrink: start large then quickly settle to 1f.
				t = 1f - (1f - t) * (1f - t);
				popScale = MathHelper.Lerp(NextTargetPopStartScale, 1f, t);
				_nextTargetPopTimer--;
			}

			// "滑索头（上半部分）"：使用 ZiplinePlayer.StartRiding 里 ZiplineStart 的锚点（origin * 16 + (24, 18)）
			Point16 origin = new Point16(targetZipline.Value.X - 1, targetZipline.Value.Y - 5);
			Vector2 headWorld = new Vector2(origin.X * 16f + 24f, origin.Y * 16f + 18f);
			Vector2 headScreen = headWorld.ToScreenPosition();

			float zoom = Main.GameZoomTarget;
			if (zoom <= 0f)
				zoom = 1f;
			float baseMargin = System.Math.Max(System.Math.Max(ring.Width, ring.Height), System.Math.Max(zipline.Width, zipline.Height)) / 2f +
				System.Math.Max(arrow.Width, arrow.Height) / 2f * ArrowScale + 10f;
			float margin = baseMargin * MathHelper.Clamp(zoom, 1f, 2.5f);

			Color yellow = Color.Yellow;
			yellow.A = 255;

			bool onScreen = headScreen.X >= margin && headScreen.X <= Main.screenWidth - margin && headScreen.Y >= margin && headScreen.Y <= Main.screenHeight - margin;
			if (onScreen)
			{
				Vector2 ringOrigin = ring.Size() / 2f;
				Vector2 ziplineOrigin = zipline.Size() / 2f;
				float rotation = Main.GlobalTimeWrappedHourly * 2.5f;
				spriteBatch.Draw(ring, headScreen, null, yellow, rotation, ringOrigin, popScale, SpriteEffects.None, 0f);
				spriteBatch.Draw(zipline, headScreen, null, yellow, 0f, ziplineOrigin, popScale, SpriteEffects.None, 0f);
			}
			else
			{
				Vector2 dir = headWorld - player.Center;
				Vector2 edgePos = GetRadarEdgeDrawPos(player, headWorld);
				edgePos.X = MathHelper.Clamp(edgePos.X, margin, Main.screenWidth - margin);
				edgePos.Y = MathHelper.Clamp(edgePos.Y, margin, Main.screenHeight - margin);
				if (dir.LengthSquared() > 0.0001f)
					dir.Normalize();
				else
					dir = new Vector2(player.direction, 0f);

				Vector2 ringOrigin = ring.Size() / 2f;
				Vector2 ziplineOrigin = zipline.Size() / 2f;
				float ringRadius = ringOrigin.Y * ArrowRingRadiusMultiplier;
				float separation = ringRadius + 14f;
				Vector2 iconPos = edgePos - dir * separation;
				iconPos.X = MathHelper.Clamp(iconPos.X, margin, Main.screenWidth - margin);
				iconPos.Y = MathHelper.Clamp(iconPos.Y, margin, Main.screenHeight - margin);
				float rotation = Main.GlobalTimeWrappedHourly * 2.5f;
				spriteBatch.Draw(ring, iconPos, null, yellow, rotation, ringOrigin, popScale, SpriteEffects.None, 0f);
				spriteBatch.Draw(zipline, iconPos, null, yellow, 0f, ziplineOrigin, popScale, SpriteEffects.None, 0f);

				Vector2 arrowOrigin = arrow.Size() / 2f;
				float arrowRotation = (float)System.Math.Atan2(dir.Y, dir.X) + MathHelper.PiOver2;
				spriteBatch.Draw(arrow, edgePos, null, yellow, arrowRotation, arrowOrigin, ArrowScale, SpriteEffects.None, 0f);
			}
		}

        public override void PostDrawFullscreenMap(ref string mouseText)
        {
            return;
        }

        private static void DrawForDirection(SpriteBatch spriteBatch, Player player, int direction)
        {
            Vector2 playerTileF = player.Center.ToTileCoordinates().ToVector2();
            List<Point16> nearestList = FindNearestZiplinesInDirection(playerTileF, direction, SearchRangeTiles, MaxIndicatorsPerSide);
			Vector2 playerWorld = player.Center;

            Texture2D iconOk = _ziplineIcon?.Value;
            Texture2D iconErr = _ziplineIconError?.Value;
            Texture2D arrow = _arrowIcon?.Value;
            if (iconOk is null || iconErr is null || arrow is null)
                return;
            float baseMargin = System.Math.Max(System.Math.Max(iconOk.Width, iconOk.Height), System.Math.Max(iconErr.Width, iconErr.Height)) / 2f +
                System.Math.Max(arrow.Width, arrow.Height) / 2f * ArrowScale + 10f;
            float zoom = Main.GameZoomTarget;
            if (zoom <= 0f)
                zoom = 1f;
            float margin = baseMargin * MathHelper.Clamp(zoom, 1f, 2.5f);
            Vector2 iconOrigin = iconOk.Size() / 2f;
            Vector2 arrowOrigin = arrow.Size() / 2f;
            float ringRadius = iconOrigin.Y * ArrowRingRadiusMultiplier;

            if (nearestList.Count == 0)
            {
				return;
            }

            int drawn = 0;
            for (int i = 0; i < nearestList.Count; i++)
            {
                Point16 ziplineTile = nearestList[i];
                Vector2 targetWorld = new Vector2((ziplineTile.X + 1.5f) * 16f, (ziplineTile.Y + 3f) * 16f);
				bool targetOnScreen = IsTargetOnScreenWorld(targetWorld);
				Vector2 between = targetWorld - playerWorld;
				Vector2 dirToTarget = between;

                Vector2 ziplineCenterTileF = new Vector2(ziplineTile.X + 1.5f, ziplineTile.Y + 3f);
                int distanceTiles = (int)Vector2.Distance(playerTileF, ziplineCenterTileF);
                bool blocked = HasObstruction(player.Center.ToTileCoordinates().X, player.Center.ToTileCoordinates().Y, (int)ziplineCenterTileF.X, (int)ziplineCenterTileF.Y);
                string text = blocked ? "路径被阻挡" : $"{distanceTiles} m";
                Color textColor = blocked ? Color.Red : Color.White;
                Texture2D iconToUse = blocked ? iconErr : iconOk;

				if (targetOnScreen)
				{
					Vector2 targetScreen = targetWorld.ToScreenPosition();
					Vector2 playerScreen = playerWorld.ToScreenPosition();
					// Draw near the zipline when it is visible.
					Vector2 n = targetScreen - playerScreen;
					if (n.LengthSquared() > 0.0001f)
						n.Normalize();
					else
						n = new Vector2(direction, 0f);

					Vector2 drawPos = targetScreen - n * (26f + drawn * 34f);
					drawPos.X = MathHelper.Clamp(drawPos.X, margin, Main.screenWidth - margin);
					drawPos.Y = MathHelper.Clamp(drawPos.Y, margin, Main.screenHeight - margin);
					DrawOnscreenIndicator(spriteBatch, iconToUse, arrow, drawPos, targetScreen, ringRadius, text, textColor);
				}
				else
				{
					if (between.LengthSquared() <= 0.0001f)
						between = new Vector2(direction, 0f);
					Vector2 edgePos = GetRadarEdgeDrawPos(player, targetWorld);
					Vector2 dir = between;
					DrawOffscreenIndicator(spriteBatch, iconToUse, arrow, edgePos, dir, ringRadius, text, textColor);
				}

				drawn++;
				if (drawn >= MaxIndicatorsPerSide)
					return;
            }
        }

		private static bool IsTargetOnScreenWorld(Vector2 targetWorld)
		{
			// BossChecklist-inspired: use a world-space rectangle that shrinks with zoom.
			float zoom = Main.GameZoomTarget;
			if (zoom < 1f)
				zoom = 1f;

			float zoomFactorX = 0.25f * (zoom - 1f);
			float zoomFactorY = 0.25f * (zoom - 1f);
			if (zoomFactorX > 0.175f)
				zoomFactorX = 0.175f;
			if (zoomFactorY > 0.175f)
				zoomFactorY = 0.175f;

			int rectPosX = (int)(Main.screenPosition.X + (Main.PendingResolutionWidth * zoomFactorX));
			int rectPosY = (int)(Main.screenPosition.Y + (Main.PendingResolutionHeight * zoomFactorY));
			int rectWidth = (int)(Main.PendingResolutionWidth * (1 - 2f * zoomFactorX));
			int rectHeight = (int)(Main.PendingResolutionHeight * (1 - 2f * zoomFactorY));

			Rectangle viewRect = new Rectangle(rectPosX, rectPosY, rectWidth, rectHeight);
			// Approx zipline size in world pixels: 3x6 tiles.
			Rectangle targetRect = new Rectangle((int)(targetWorld.X - 48f / 2f), (int)(targetWorld.Y - 96f / 2f), 48, 96);
			return viewRect.Intersects(targetRect);
		}

		private static Vector2 GetRadarEdgeDrawPos(Player player, Vector2 targetWorld)
		{
			// Ported from BossChecklist BossRadarUI.SetDrawPos
			Vector2 between = targetWorld - player.Center;
			if (between.X == 0f)
				between.X = 0.0001f;
			if (between.Y == 0f)
				between.Y = 0.0001f;
			if (player.gravDir != 1f)
				between.Y = -between.Y;

			float slope = between.Y / between.X;
			Vector2 pad = new Vector2((Main.screenWidth + 48f) / 2f, (Main.screenHeight + 96f) / 2f);
			Vector2 ldrawPos = Vector2.Zero;

			// first iteration (clamp Y)
			if (between.Y > 0f)
				ldrawPos.Y = between.Y > pad.Y ? pad.Y : between.Y;
			else
				ldrawPos.Y = between.Y < -pad.Y ? -pad.Y : between.Y;
			ldrawPos.X = ldrawPos.Y / slope;

			// second iteration (clamp X)
			if (ldrawPos.X > 0f) {
				if (ldrawPos.X > pad.X)
					ldrawPos.X = pad.X;
			}
			else {
				if (ldrawPos.X <= -pad.X)
					ldrawPos.X = -pad.X;
			}
			ldrawPos.Y = ldrawPos.X * slope;

			// revert offset
			ldrawPos += new Vector2(pad.X, pad.Y);
			// move from center-to-center to top-left of draw pos
			ldrawPos -= new Vector2(48f, 96f) / 2f;
			// return center point (our drawing expects center)
			return ldrawPos + new Vector2(48f, 96f) / 2f;
		}

		private static void DrawOffscreenIndicator(SpriteBatch spriteBatch, Texture2D icon, Texture2D arrow, Vector2 edgePos, Vector2 direction, float ringRadius, string text, Color textColor)
		{
			if (direction.LengthSquared() > 0.0001f)
				direction.Normalize();
			else
				direction = Vector2.UnitX;

			Vector2 iconOrigin = icon.Size() / 2f;
			Vector2 arrowOrigin = arrow.Size() / 2f;
			Vector2 iconPos = edgePos - direction * ringRadius;
			spriteBatch.Draw(icon, iconPos, null, Color.White, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);

			float arrowRotation = (float)System.Math.Atan2(direction.Y, direction.X) + MathHelper.PiOver2;
			spriteBatch.Draw(arrow, edgePos, null, Color.White, arrowRotation, arrowOrigin, ArrowScale, SpriteEffects.None, 0f);

			Vector2 textPos = iconPos + new Vector2(0f, iconOrigin.Y + 8f);
			Utils.DrawBorderString(spriteBatch, text ?? string.Empty, textPos, textColor, 1f, 0.5f, 0f);
		}

		private static void DrawOnscreenIndicator(SpriteBatch spriteBatch, Texture2D icon, Texture2D arrow, Vector2 drawPos, Vector2 targetScreen, float ringRadius, string text, Color textColor)
		{
			Vector2 v = targetScreen - drawPos;
			Vector2 dir = v;
			if (dir.LengthSquared() > 0.0001f)
				dir.Normalize();
			else
				dir = Vector2.UnitX;

			Vector2 iconOrigin = icon.Size() / 2f;
			Vector2 arrowOrigin = arrow.Size() / 2f;
			spriteBatch.Draw(icon, drawPos, null, Color.White, 0f, iconOrigin, 1f, SpriteEffects.None, 0f);

			Vector2 arrowPos = drawPos + dir * ringRadius;
			float arrowRotation = (float)System.Math.Atan2(dir.Y, dir.X) + MathHelper.PiOver2;
			spriteBatch.Draw(arrow, arrowPos, null, Color.White, arrowRotation, arrowOrigin, ArrowScale, SpriteEffects.None, 0f);

			Vector2 textPos = drawPos + new Vector2(0f, iconOrigin.Y + 8f);
			Utils.DrawBorderString(spriteBatch, text ?? string.Empty, textPos, textColor, 1f, 0.5f, 0f);
		}

        private static bool IsOnScreen(Vector2 screenPosition, float margin)
        {
            float uiScale = Main.UIScale;
            if (uiScale <= 0f)
                uiScale = 1f;
            float w = Main.screenWidth / uiScale;
            float h = Main.screenHeight / uiScale;
            return screenPosition.X >= margin && screenPosition.X <= w - margin &&
                screenPosition.Y >= margin && screenPosition.Y <= h - margin;
        }

		private static Vector2 GetEdgePositionFromRay(Vector2 origin, Vector2 direction, float margin, Vector2 screenSize)
		{
			if (direction.LengthSquared() < 0.0001f)
				return origin;

			float left = margin;
			float right = screenSize.X - margin;
			float top = margin;
			float bottom = screenSize.Y - margin;

			float tMin = float.PositiveInfinity;
			Vector2 best = origin;

			if (direction.X != 0f)
			{
				float t1 = (left - origin.X) / direction.X;
				float y1 = origin.Y + direction.Y * t1;
				if (t1 > 0f && y1 >= top && y1 <= bottom && t1 < tMin)
				{
					tMin = t1;
					best = new Vector2(left, y1);
				}

				float t2 = (right - origin.X) / direction.X;
				float y2 = origin.Y + direction.Y * t2;
				if (t2 > 0f && y2 >= top && y2 <= bottom && t2 < tMin)
				{
					tMin = t2;
					best = new Vector2(right, y2);
				}
			}

			if (direction.Y != 0f)
			{
				float t3 = (top - origin.Y) / direction.Y;
				float x3 = origin.X + direction.X * t3;
				if (t3 > 0f && x3 >= left && x3 <= right && t3 < tMin)
				{
					tMin = t3;
					best = new Vector2(x3, top);
				}

				float t4 = (bottom - origin.Y) / direction.Y;
				float x4 = origin.X + direction.X * t4;
				if (t4 > 0f && x4 >= left && x4 <= right && t4 < tMin)
				{
					tMin = t4;
					best = new Vector2(x4, bottom);
				}
			}

			best.X = MathHelper.Clamp(best.X, left, right);
			best.Y = MathHelper.Clamp(best.Y, top, bottom);
			return best;
		}

		private static List<Point16> FindNearestZiplinesInDirection(Vector2 from, int direction, int searchRange, int maxCount)
		{
			List<(Point16 pos, float dist)> found = new List<(Point16 pos, float dist)>(32);
			int tileType = ModContent.TileType<LongDistanceZiplineTile>();

			int startX = (int)from.X - searchRange;
			int endX = (int)from.X + searchRange;
			int yRange = searchRange * 3;
			int startY = (int)from.Y - yRange;
			int endY = (int)from.Y + yRange;

			startX = Utils.Clamp(startX, 0, Main.maxTilesX - 1);
			endX = Utils.Clamp(endX, 0, Main.maxTilesX - 1);
			startY = Utils.Clamp(startY, 0, Main.maxTilesY - 1);
			endY = Utils.Clamp(endY, 0, Main.maxTilesY - 1);

			for (int x = startX; x <= endX; x++)
			{
				for (int y = startY; y <= endY; y++)
				{
					if (!WorldGen.InWorld(x, y))
						continue;

					Tile tile = Main.tile[x, y];
					if (!tile.HasTile || tile.TileType != tileType)
						continue;

					const int ziplineWidth = 3;
					const int ziplineHeight = 6;
					const int ziplineAnimationFrameHeight = 135;

					int frameX = (tile.TileFrameX / 17) % ziplineWidth;
					int localFrameY = tile.TileFrameY;
					if (ziplineAnimationFrameHeight > 0)
						localFrameY %= ziplineAnimationFrameHeight;
					int frameY = (localFrameY / 17) % ziplineHeight;

					if (frameX != 0 || frameY != 0)
						continue;

					Point16 originPos = new Point16(x, y);
					Vector2 centerPos = new Vector2(originPos.X + 1.5f, originPos.Y + 3f);
					Vector2 delta = centerPos - from;
					if ((direction < 0 && delta.X >= 0f) || (direction > 0 && delta.X <= 0f))
						continue;

					float distance = Vector2.Distance(from, centerPos);
					found.Add((originPos, distance));
				}
			}

			found.Sort((a, b) => a.dist.CompareTo(b.dist));
			List<Point16> result = new List<Point16>(System.Math.Min(maxCount, found.Count));
			Point16 last = default;
			bool hasLast = false;
			for (int i = 0; i < found.Count && result.Count < maxCount; i++)
			{
				Point16 p = found[i].pos;
				if (hasLast && p.X == last.X && p.Y == last.Y)
					continue;
				result.Add(p);
				last = p;
				hasLast = true;
			}
			return result;
		}

        private static bool HasObstruction(int x1, int y1, int x2, int y2)
        {
            Vector2 start = new Vector2(x1, y1);
            Vector2 end = new Vector2(x2, y2);
            Vector2 direction = end - start;
            float distance = direction.Length();
            if (distance <= 0.0001f)
                return false;

            direction.Normalize();
            int ziplineTileType = ModContent.TileType<LongDistanceZiplineTile>();

            for (float d = 0; d < distance; d += 0.5f)
            {
                Vector2 checkPos = start + direction * d;
                int tileX = (int)checkPos.X;
                int tileY = (int)checkPos.Y;
                if (!WorldGen.InWorld(tileX, tileY))
                    continue;
                Tile tile = Main.tile[tileX, tileY];
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType] && tile.TileType != ziplineTileType)
                    return true;
            }

            return false;
        }
    }
}
