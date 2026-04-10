using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;
using EndfieldZipline.Common.Configs;
using EndfieldZipline.Content.Items;

namespace EndfieldZipline.Content.Players
{
	public class ZiplinePlayer : ModPlayer
	{
		private const float ZiplineVisualYOffset = 6f;
		private const bool DrawStraightZiplineVisual = true;
		private const float ZiplineLocalSagWidth = 0.14f;
		private const float ZiplineDynamicSagMax = 18f;
		private const float ZiplineDynamicSagSpring = 0.18f;
		private const float ZiplineDynamicSagDamping = 0.82f;

		private static readonly SoundStyle ZiplineRideLoopSound = new SoundStyle("EndfieldZipline/Content/Sounds/滑行中")
		{
			IsLooped = true,
			PauseBehavior = PauseBehavior.StopWhenGamePaused,
			SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest,
			MaxInstances = 1
		};

		public bool IsOnZipline = false;
		public bool IsRiding = false;
		public Point16 CurrentZiplinePos;
		public Vector2 ZiplineStart;
		public Vector2 ZiplineEnd;
		public float ZiplineProgress = 0f;
		public float ZiplinePixelsPerFrame = 8f;
		public float ZiplineDistance = 0f;

		private float _dynamicSagOffset;
		private float _dynamicSagVelocity;

		private bool forceZiplinePosition;
		private Vector2 forcedZiplinePosition;

		private bool lastControlUseTile = false;
		private bool lastQuickMovePressed = false;
		private int obstructionMessageCooldown = 0;
		private SlotId _ziplineRideLoopSlot;
		public Item FuelSlotItem;
		public Item ZiplineFrameSlotItem;

		public override void Initialize()
		{
			FuelSlotItem = new Item();
			FuelSlotItem.SetDefaults(0);
			ZiplineFrameSlotItem = new Item();
			ZiplineFrameSlotItem.SetDefaults(0);
		}

		public override void SaveData(TagCompound tag)
		{
			tag["FuelSlotItem"] = ItemIO.Save(FuelSlotItem);
			tag["ZiplineFrameSlotItem"] = ItemIO.Save(ZiplineFrameSlotItem);
		}

		public override void LoadData(TagCompound tag)
		{
			FuelSlotItem = tag.ContainsKey("FuelSlotItem") ? ItemIO.Load(tag.GetCompound("FuelSlotItem")) : new Item();
			if (FuelSlotItem != null && FuelSlotItem.type == 0)
				FuelSlotItem.SetDefaults(0);
			ZiplineFrameSlotItem = tag.ContainsKey("ZiplineFrameSlotItem") ? ItemIO.Load(tag.GetCompound("ZiplineFrameSlotItem")) : new Item();
			if (ZiplineFrameSlotItem != null && ZiplineFrameSlotItem.type == 0)
				ZiplineFrameSlotItem.SetDefaults(0);
		}

		private static bool IsKeyDownFromName(string keyName)
		{
			if (string.IsNullOrWhiteSpace(keyName))
				return false;
			if (!Enum.TryParse(keyName.Trim(), true, out Keys key))
				return false;
			return Main.keyState.IsKeyDown(key);
		}

		public Point16? GetNextZiplineTarget(int direction)
		{
			if (!IsOnZipline)
				return null;
			if (IsRiding)
				return CurrentZiplinePos;
			if (direction == 0)
				direction = Player.direction;
			return FindNearestZiplineInDirection(direction);
		}

		private static bool IsKeyJustPressedFromName(string keyName)
		{
			if (string.IsNullOrWhiteSpace(keyName))
				return false;
			if (!Enum.TryParse(keyName.Trim(), true, out Keys key))
				return false;
			return Main.keyState.IsKeyDown(key) && !Main.oldKeyState.IsKeyDown(key);
		}

		public void AttachToZipline(Point16 ziplinePos)
		{
			IsOnZipline = true;
			IsRiding = false;
			CurrentZiplinePos = ziplinePos;

			Point16 origin = new Point16(ziplinePos.X - 1, ziplinePos.Y - 5);
			Vector2 ziplineWorldPos = new Vector2(origin.X * 16 + 24, origin.Y * 16 + 48);
			Player.position = ziplineWorldPos - new Vector2(Player.width / 2, Player.height);
			Player.velocity = Vector2.Zero;
			Player.fallStart = (int)(Player.position.Y / 16f);
		}

		private void UpdateDynamicSag(float progress01)
		{
			float p = MathHelper.Clamp(progress01, 0f, 1f);
			float desired = (float)System.Math.Sin(p * System.Math.PI) * ZiplineDynamicSagMax;
			float accel = (desired - _dynamicSagOffset) * ZiplineDynamicSagSpring;
			_dynamicSagVelocity += accel;
			_dynamicSagVelocity *= ZiplineDynamicSagDamping;
			_dynamicSagOffset += _dynamicSagVelocity;
		}

		public void StartRiding(Point16 start, Point16 end)
		{
			IsRiding = true;
			CurrentZiplinePos = end;

			Point16 startOrigin = new Point16(start.X - 1, start.Y - 5);
			Point16 endOrigin = new Point16(end.X - 1, end.Y - 5);
			ZiplineStart = new Vector2(startOrigin.X * 16 + 24, startOrigin.Y * 16 + 18);
			ZiplineEnd = new Vector2(endOrigin.X * 16 + 24, endOrigin.Y * 16 + 18);
			ZiplineProgress = 0f;
			EndfieldZiplineConfig config = ModContent.GetInstance<EndfieldZiplineConfig>();
			Item frameItem = ZiplineFrameSlotItem;
			int frameType = (frameItem != null && !frameItem.IsAir) ? frameItem.type : 0;

			if (config is not null && config.SimpleModeEnabled)
			{
				ZiplinePixelsPerFrame = config.ZiplineMoveSpeed switch
				{
					EndfieldZiplineConfig.ZiplineMoveSpeedPreset.Teleport => 999999f,
					EndfieldZiplineConfig.ZiplineMoveSpeedPreset.UltraFast => 16f,
					EndfieldZiplineConfig.ZiplineMoveSpeedPreset.Fast => 12f,
					_ => 8f
				};
			}
			else
			{
				if (frameType == ModContent.ItemType<CommonZiplineFrame>())
					ZiplinePixelsPerFrame = 12f;
				else if (frameType == ModContent.ItemType<AdamantiteZiplineFrame>())
					ZiplinePixelsPerFrame = 16f;
				else if (frameType == ModContent.ItemType<TitaniumZiplineFrame>())
					ZiplinePixelsPerFrame = 16f;
				else if (frameType == ModContent.ItemType<MechanicalZiplineFrame>())
					ZiplinePixelsPerFrame = 600f;
				else if (frameType == ModContent.ItemType<NightglowZiplineFrame>())
					ZiplinePixelsPerFrame = 999999f;
				else if (frameType == ModContent.ItemType<BoulderZiplineFrame>())
					ZiplinePixelsPerFrame = 20f;
				else
					ZiplinePixelsPerFrame = 8f;
			}

			ZiplineDistance = Vector2.Distance(ZiplineStart, ZiplineEnd);
			_dynamicSagOffset = 0f;
			_dynamicSagVelocity = 0f;

			if (ZiplineEnd.X > ZiplineStart.X)
			{
				Player.direction = 1;
			}
			else if (ZiplineEnd.X < ZiplineStart.X)
			{
				Player.direction = -1;
			}

			Player.velocity = Vector2.Zero;
			Player.fallStart = (int)(Player.position.Y / 16f);

			if (_ziplineRideLoopSlot.IsValid && SoundEngine.TryGetActiveSound(_ziplineRideLoopSlot, out var existingSound))
				existingSound.Stop();
			_ziplineRideLoopSlot = default;
			if (config is not null && !config.DisableZiplineSounds)
				_ziplineRideLoopSlot = SoundEngine.PlaySound(ZiplineRideLoopSound, Player.Center);
		}

		public void DetachFromZipline()
		{
			IsOnZipline = false;
			IsRiding = false;
			Player.pulley = false;

			if (_ziplineRideLoopSlot.IsValid && SoundEngine.TryGetActiveSound(_ziplineRideLoopSlot, out var activeSound))
				activeSound.Stop();
			_ziplineRideLoopSlot = default;
		}

		public override void PreUpdate()
		{
			forceZiplinePosition = false;

			if (obstructionMessageCooldown > 0)
			{
				obstructionMessageCooldown--;
			}

			if (IsOnZipline)
			{
				if (WorldGen.InWorld(CurrentZiplinePos.X, CurrentZiplinePos.Y))
				{
					Tile tile = Main.tile[CurrentZiplinePos.X, CurrentZiplinePos.Y];
					if (!tile.HasTile || tile.TileType != ModContent.TileType<Tiles.LongDistanceZiplineTile>())
					{
						DetachFromZipline();
						return;
					}
				}
				else
				{
					DetachFromZipline();
					return;
				}
			}

			EndfieldZiplineConfig config = ModContent.GetInstance<EndfieldZiplineConfig>();
			if (config is not null && config.DisableZiplineSounds)
			{
				if (_ziplineRideLoopSlot.IsValid && SoundEngine.TryGetActiveSound(_ziplineRideLoopSlot, out var activeSound))
					activeSound.Stop();
				_ziplineRideLoopSlot = default;
			}
			string quickMoveKeyName = config.ZiplineQuickMoveKey;
			bool quickMoveJustPressed = IsKeyJustPressedFromName(quickMoveKeyName);
			if (!quickMoveJustPressed && Player.controlHook && !lastQuickMovePressed)
			{
				if (!IsKeyDownFromName(quickMoveKeyName))
					quickMoveJustPressed = true;
			}
			lastQuickMovePressed = IsKeyDownFromName(quickMoveKeyName) || Player.controlHook;

			if (IsOnZipline && !IsRiding && quickMoveJustPressed)
			{
				Point16? nearestZipline = FindNearestZiplineInDirection(Player.direction);
				if (nearestZipline.HasValue)
				{
					Point16 currentOrigin = new Point16(CurrentZiplinePos.X - 1, CurrentZiplinePos.Y - 5);
					Point16 targetOrigin = new Point16(nearestZipline.Value.X - 1, nearestZipline.Value.Y - 5);
					int currentCenterY = currentOrigin.Y + 3;
					int targetCenterY = targetOrigin.Y + 3;

					if (!HasObstructionFromPlayer(currentOrigin.X, currentCenterY, targetOrigin.X, targetCenterY))
					{
						StartRiding(CurrentZiplinePos, nearestZipline.Value);
					}
					else
					{
						if (obstructionMessageCooldown <= 0)
						{
							Main.NewText(Language.GetTextValue("Mods.EndfieldZipline.UI.ZiplineObstructed"), (byte)255, (byte)100, (byte)100);
							obstructionMessageCooldown = 60;
						}
					}
				}
			}

			bool currentControlUseTile = Player.controlUseTile && !Player.mouseInterface;
			bool justPressed = currentControlUseTile && !lastControlUseTile;
			lastControlUseTile = currentControlUseTile;

			if (IsOnZipline && !IsRiding && justPressed)
			{
				int tileX = (int)((Main.mouseX + Main.screenPosition.X) / 16f);
				int tileY = (int)((Main.mouseY + Main.screenPosition.Y) / 16f);

				if (WorldGen.InWorld(tileX, tileY))
				{
					Tile tile = Main.tile[tileX, tileY];

					if (tile.HasTile && tile.TileType == ModContent.TileType<Tiles.LongDistanceZiplineTile>())
					{
						const int ziplineWidth = 3;
						const int ziplineHeight = 6;
						const int ziplineAnimationFrameHeight = 135;
						int frameX = (tile.TileFrameX / 17) % ziplineWidth;
						int localFrameY = tile.TileFrameY;
						if (ziplineAnimationFrameHeight > 0)
							localFrameY %= ziplineAnimationFrameHeight;
						int frameY = (localFrameY / 17) % ziplineHeight;
						Point16 originPos = new Point16(tileX - frameX, tileY - frameY);
						Point16 ziplinePos = new Point16(originPos.X + 1, originPos.Y + 5);

						if (CurrentZiplinePos != ziplinePos)
						{
							Point16 currentOrigin = new Point16(CurrentZiplinePos.X - 1, CurrentZiplinePos.Y - 5);
							Point16 targetOrigin = new Point16(ziplinePos.X - 1, ziplinePos.Y - 5);
							int currentCenterY = currentOrigin.Y + 3;
							int targetCenterY = targetOrigin.Y + 3;
							if (!HasObstructionFromPlayer(currentOrigin.X, currentCenterY, targetOrigin.X, targetCenterY))
							{
								StartRiding(CurrentZiplinePos, ziplinePos);
							}
							else
							{
								if (obstructionMessageCooldown <= 0)
								{
									Main.NewText(Language.GetTextValue("Mods.EndfieldZipline.UI.ZiplineObstructed"), (byte)255, (byte)100, (byte)100);
									obstructionMessageCooldown = 60;
								}
							}
						}
					}
				}
			}

			if (IsOnZipline)
			{
				Player.ropeCount = 10;
				bool ropePhysics = config != null && config.ZiplineRopePhysicsEffect;

				// Keep vanilla rope/chain animation (pulley pose) but prevent vanilla pulley movement from taking over.
				Player.controlLeft = false;
				Player.controlRight = false;
				Player.controlUp = false;
				Player.controlDown = false;

				if (IsRiding)
				{
					Player.noKnockback = true;
					Player.ghost = false;
					Player.immune = true;
					Player.immuneTime = 2;
					Player.immuneNoBlink = true;
					if (Player.hurtCooldowns != null)
					{
						for (int i = 0; i < Player.hurtCooldowns.Length; i++)
							Player.hurtCooldowns[i] = 2;
					}

					if (ZiplineDistance > 0)
					{
						float progressIncrement = ZiplinePixelsPerFrame / ZiplineDistance;
						ZiplineProgress += progressIncrement;
					}
					else
					{
						ZiplineProgress = 1f;
					}

					if (ropePhysics)
						UpdateDynamicSag(ZiplineProgress);
					else {
						_dynamicSagOffset = 0f;
						_dynamicSagVelocity = 0f;
					}
					Vector2 currentPos = Vector2.Lerp(ZiplineStart, ZiplineEnd, ZiplineProgress);
					forcedZiplinePosition = currentPos - new Vector2(Player.width / 2, Player.height / 2) + new Vector2(0f, ZiplineVisualYOffset + _dynamicSagOffset);
					forceZiplinePosition = true;
					Player.position = forcedZiplinePosition;
					Player.oldPosition = Player.position;
					Player.gfxOffY = 0f;
					Player.fallStart = (int)(Player.position.Y / 16f);
					Player.velocity = Vector2.Zero;

					if (!(config is not null && config.DisableZiplineSounds))
					{
						if (_ziplineRideLoopSlot.IsValid && SoundEngine.TryGetActiveSound(_ziplineRideLoopSlot, out var rideSound))
							rideSound.Position = Player.Center;
					}

					DrawZiplineRopeWithDust();

					if (Main.rand.NextBool(2))
					{
						Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height,
							Terraria.ID.DustID.Smoke, 0f, 0f, 100, default, 0.6f);
						dust.velocity *= 0.2f;
						dust.noGravity = true;
					}

					if (ZiplineProgress >= 1f)
					{
						if (ZiplineFrameSlotItem != null && !ZiplineFrameSlotItem.IsAir
							&& ZiplineFrameSlotItem.type == ModContent.ItemType<BoulderZiplineFrame>())
						{
							Player.KillMe(PlayerDeathReason.ByCustomReason(Language.GetTextValue("Mods.EndfieldZipline.UI.BoulderDeathReason", Player.name)), 9999.0, 0);
							DetachFromZipline();
							return;
						}

						if (_ziplineRideLoopSlot.IsValid && SoundEngine.TryGetActiveSound(_ziplineRideLoopSlot, out var activeSound))
							activeSound.Stop();
						_ziplineRideLoopSlot = default;

						if (!(config is not null && config.DisableZiplineSounds))
							SoundEngine.PlaySound(new SoundStyle("EndfieldZipline/Content/Sounds/到达目的地"), Player.Center);
						IsRiding = false;
						Player.ghost = false;

						Point16 origin = new Point16(CurrentZiplinePos.X - 1, CurrentZiplinePos.Y - 5);
						Vector2 ziplineWorldPos = new Vector2(origin.X * 16 + 24, origin.Y * 16 + 48);
						forcedZiplinePosition = ziplineWorldPos - new Vector2(Player.width / 2, Player.height) + new Vector2(0f, ZiplineVisualYOffset);
						forceZiplinePosition = true;
						Player.position = forcedZiplinePosition;
					}

					if (Player.controlJump)
					{
						DetachFromZipline();
						Player.ghost = false;
					}
				}
				else
				{
					if (_ziplineRideLoopSlot.IsValid && SoundEngine.TryGetActiveSound(_ziplineRideLoopSlot, out var activeSound))
						activeSound.Stop();
					_ziplineRideLoopSlot = default;

					Player.ghost = false;
					Point16 origin = new Point16(CurrentZiplinePos.X - 1, CurrentZiplinePos.Y - 5);
					Vector2 ziplineWorldPos = new Vector2(origin.X * 16 + 24, origin.Y * 16 + 48);
					if (ropePhysics)
						UpdateDynamicSag(0.5f);
					else {
						_dynamicSagOffset = 0f;
						_dynamicSagVelocity = 0f;
					}
					Player.position = ziplineWorldPos - new Vector2(Player.width / 2, Player.height) + new Vector2(0f, ZiplineVisualYOffset + _dynamicSagOffset);
					Player.velocity = Vector2.Zero;

					if (Player.controlJump)
					{
						DetachFromZipline();
					}
				}
			}
		}

		public override void PostUpdate()
		{
			if (forceZiplinePosition)
			{
				Player.position = forcedZiplinePosition;
				Player.oldPosition = Player.position;
				Player.gfxOffY = 0f;
				Player.fallStart = (int)(Player.position.Y / 16f);
				Player.velocity = Vector2.Zero;
			}

			if (IsOnZipline)
			{
				Player.gravity = 0f;
				Player.maxFallSpeed = 0f;

				// Compute vanilla rope/chain climbing frames, but restore pulley state so the vanilla pulley icon won't show.
				bool oldPulley = Player.pulley;
				byte oldPulleyDir = Player.pulleyDir;
				Player.pulley = true;
				Player.pulleyDir = (byte)2;
				Player.PlayerFrame();
				Player.pulley = oldPulley;
				Player.pulleyDir = oldPulleyDir;
			}
		}

		private Point16? FindNearestZiplineInDirection(int direction)
		{
			Point16? nearestZipline = null;
			float nearestDistance = float.MaxValue;
			int searchRange = 100;

			Point16 currentOrigin = new Point16(CurrentZiplinePos.X - 1, CurrentZiplinePos.Y - 5);

			int startX = direction > 0 ? CurrentZiplinePos.X + 1 : CurrentZiplinePos.X - searchRange;
			int endX = direction > 0 ? CurrentZiplinePos.X + searchRange : CurrentZiplinePos.X - 1;

			for (int x = startX; x <= endX; x++)
			{
				for (int y = CurrentZiplinePos.Y - searchRange; y <= CurrentZiplinePos.Y + searchRange; y++)
				{
					if (!WorldGen.InWorld(x, y))
						continue;

					Tile tile = Main.tile[x, y];
					if (tile.HasTile && tile.TileType == ModContent.TileType<Tiles.LongDistanceZiplineTile>())
					{
						const int ziplineWidth = 3;
						const int ziplineHeight = 6;
						const int ziplineAnimationFrameHeight = 135;
						int frameX = (tile.TileFrameX / 17) % ziplineWidth;
						int localFrameY = tile.TileFrameY;
						if (ziplineAnimationFrameHeight > 0)
							localFrameY %= ziplineAnimationFrameHeight;
						int frameY = (localFrameY / 17) % ziplineHeight;
						Point16 originPos = new Point16(x - frameX, y - frameY);
						Point16 ziplinePos = new Point16(originPos.X + 1, originPos.Y + 5);

						if (ziplinePos == CurrentZiplinePos)
							continue;

						if ((direction > 0 && ziplinePos.X <= CurrentZiplinePos.X) || (direction < 0 && ziplinePos.X >= CurrentZiplinePos.X))
							continue;

						Point16 targetOrigin = new Point16(ziplinePos.X - 1, ziplinePos.Y - 5);
						float distance = Vector2.Distance(
							new Vector2(currentOrigin.X, currentOrigin.Y),
							new Vector2(targetOrigin.X, targetOrigin.Y)
						);

						if (distance < nearestDistance)
						{
							nearestDistance = distance;
							nearestZipline = ziplinePos;
						}
					}
				}
			}

			return nearestZipline;
		}

		private bool HasObstructionFromPlayer(int x1, int y1, int x2, int y2)
		{
			Vector2 start = new Vector2(x1, y1);
			Vector2 end = new Vector2(x2, y2);
			Vector2 direction = end - start;
			float distance = direction.Length();
			direction.Normalize();

			for (float d = 0; d < distance; d += 0.5f)
			{
				Vector2 checkPos = start + direction * d;
				int tileX = (int)checkPos.X;
				int tileY = (int)checkPos.Y;

				if (WorldGen.InWorld(tileX, tileY))
				{
					Tile tile = Main.tile[tileX, tileY];
					if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]
						&& tile.TileType != ModContent.TileType<Tiles.LongDistanceZiplineTile>())
					{
						return true;
					}
				}
			}

			return false;
		}

		public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
		{
		}

		private void DrawZiplineRopeWithDust()
		{
			EndfieldZiplineConfig config = ModContent.GetInstance<EndfieldZiplineConfig>();
			bool ropePhysics = config != null && config.ZiplineRopePhysicsEffect;
			float distance = Vector2.Distance(ZiplineStart, ZiplineEnd);
			int segments = (int)(distance / 12);

			if (segments <= 0)
				segments = 1;
			Color ropeColor = config.ZiplineRopeColor;
			Vector3 lightColor = ropeColor.ToVector3() * 0.55f;

			if (DrawStraightZiplineVisual)
			{
				bool spawnThisFrame = (Main.GameUpdateCount % 2u) == 0u;
				for (int i = 0; i <= segments; i++)
				{
					float progress = i / (float)segments;
					Vector2 basePosition = Vector2.Lerp(ZiplineStart, ZiplineEnd, progress);

					Vector2 drawPos = basePosition;
					float weight = 0f;
					if (ropePhysics)
					{
						float w = ZiplineLocalSagWidth;
						float diff = progress - ZiplineProgress;
						weight = (w <= 0.0001f) ? 0f : (float)System.Math.Exp(-(diff * diff) / (w * w));
						float localSag = _dynamicSagOffset * weight;
						drawPos.Y += localSag;
					}
					if (i % 3 == 0)
					{
						Lighting.AddLight(drawPos, lightColor);
					}

					// Baseline rope: stable, clearly visible line.
					if ((i % 2 == 0) && (spawnThisFrame || (i % 6 == 0)))
					{
						Dust dust = Dust.NewDustPerfect(basePosition, DustID.TintableDust, Vector2.Zero, 120, ropeColor, 1.0f);
						dust.noGravity = true;
						dust.noLight = false;
						dust.velocity = Vector2.Zero;
						dust.fadeIn = 0f;
					}

					// Local sag overlay: sparse + very transparent to avoid leaving multiple "lines".
					if (ropePhysics && spawnThisFrame && (i % 4 == 0) && weight > 0.35f)
					{
						Dust dust = Dust.NewDustPerfect(drawPos, DustID.TintableDust, Vector2.Zero, 235, ropeColor, 0.75f);
						dust.noGravity = true;
						dust.noLight = false;
						dust.velocity = Vector2.Zero;
						dust.fadeIn = 0f;
					}
				}
			}

			Vector2 playerPos = Vector2.Lerp(ZiplineStart, ZiplineEnd, ZiplineProgress) + new Vector2(0f, _dynamicSagOffset);
			Lighting.AddLight(playerPos, lightColor * 1.5f);
			if ((Main.GameUpdateCount % 2u) == 0u)
			{
				for (int i = 0; i < 3; i++)
				{
					Dust dust = Dust.NewDustPerfect(playerPos, DustID.TintableDust, Vector2.Zero, 160, ropeColor, 1.2f);
					dust.noGravity = true;
					dust.velocity = Vector2.Zero;
				}
			}
		}
	}
}
