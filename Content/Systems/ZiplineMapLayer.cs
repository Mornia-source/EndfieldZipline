using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;
using EndfieldZipline.Content.Tiles;
using EndfieldZipline.Common.Configs;
using EndfieldZipline.Content.Players;
using EndfieldZipline.Content.Items;
using Terraria.ID;
using Terraria.Localization;

namespace EndfieldZipline.Content.Systems
{
	public sealed class ZiplineMapLayer : ModMapLayer
	{
		private static Asset<Texture2D> _ziplineIcon;

		public override Position GetDefaultPosition() => new Before(IMapLayer.Pings);

		public override void Load()
		{
			if (Main.dedServ)
				return;

			_ziplineIcon = ModContent.Request<Texture2D>("EndfieldZipline/Content/Images/Icon/滑索缩略", AssetRequestMode.ImmediateLoad);
		}

		public override void Unload()
		{
			_ziplineIcon = null;
		}

		public override void Draw(ref MapOverlayDrawContext context, ref string text)
		{
			if (Main.gameMenu || !Main.mapFullscreen)
				return;
			if (!ModContent.GetInstance<EndfieldZiplineConfig>().ShowZiplineIconsOnFullscreenMap)
				return;

			Texture2D icon = _ziplineIcon?.Value;
			if (icon is null)
				return;

			int tileType = ModContent.TileType<LongDistanceZiplineTile>();
			const int ziplineWidth = 3;
			const int ziplineHeight = 6;
			const int ziplineAnimationFrameHeight = 135;

			Vector2 centerTiles = Main.mapFullscreenPos;
			float mapScale = Main.mapFullscreenScale;
			if (mapScale <= 0f)
				return;

			int halfTilesX = (int)(Main.screenWidth / mapScale / 2f) + 30;
			int halfTilesY = (int)(Main.screenHeight / mapScale / 2f) + 30;

			int startX = (int)centerTiles.X - halfTilesX;
			int endX = (int)centerTiles.X + halfTilesX;
			int startY = (int)centerTiles.Y - halfTilesY;
			int endY = (int)centerTiles.Y + halfTilesY;

			startX = Utils.Clamp(startX, 0, Main.maxTilesX - 1);
			endX = Utils.Clamp(endX, 0, Main.maxTilesX - 1);
			startY = Utils.Clamp(startY, 0, Main.maxTilesY - 1);
			endY = Utils.Clamp(endY, 0, Main.maxTilesY - 1);

			const float scaleIfNotSelected = 0.75f;
			const float scaleIfSelected = 1.05f;

			for (int x = startX; x <= endX; x++)
			{
				for (int y = startY; y <= endY; y++)
				{
					Tile tile = Main.tile[x, y];
					if (!tile.HasTile || tile.TileType != tileType)
						continue;

					int frameX = (tile.TileFrameX / 17) % ziplineWidth;
					int localFrameY = tile.TileFrameY;
					if (ziplineAnimationFrameHeight > 0f)
						localFrameY %= ziplineAnimationFrameHeight;
					int frameY = (localFrameY / 17) % ziplineHeight;
					if (frameX != 0 || frameY != 0)
						continue;

					Vector2 tilePosition = new Vector2(x + 1.5f, y + 3f);
					var result = context.Draw(icon, tilePosition, Color.White, new SpriteFrame(1, 1, 0, 0), scaleIfNotSelected, scaleIfSelected, Alignment.Center);
					if (result.IsMouseOver)
					{
						text = Language.GetTextValue("Mods.EndfieldZipline.UI.MapZiplineHoverText");

						Player player = Main.LocalPlayer;
						if (player != null && player.active)
						{
							ZiplinePlayer ziplinePlayer = player.GetModPlayer<ZiplinePlayer>();
							Item frameItem = ziplinePlayer?.ZiplineFrameSlotItem;
							if (frameItem != null && !frameItem.IsAir && frameItem.type == ModContent.ItemType<NightglowZiplineFrame>())
							{
								if (Main.mouseLeft && Main.mouseLeftRelease)
								{
									Point16 ziplinePos = new Point16(x + 1, y + 5);
									Point16 origin = new Point16(ziplinePos.X - 1, ziplinePos.Y - 5);
									Vector2 worldPos = new Vector2(origin.X * 16 + 24, origin.Y * 16 + 48);
									player.Teleport(worldPos - new Vector2(player.width / 2f, player.height), 1);
									if (Main.netMode == NetmodeID.MultiplayerClient)
										NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, player.whoAmI, worldPos.X, worldPos.Y, 1);
									Main.mapFullscreen = false;
									Main.mouseLeftRelease = false;
								}
							}
						}
					}
				}
			}
		}
	}
}
