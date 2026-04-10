using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using EndfieldZipline.Content.Players;

namespace EndfieldZipline.Content.Tiles
{
	public class LongDistanceZiplineTile : ModTile
	{
		public override string Texture => "EndfieldZipline/Content/Images/Item/长距离滑索-Sheet_gap1";

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = false;

			TileID.Sets.HasOutlines[Type] = false;

			TileObjectData.newTile.CopyFrom(TileObjectData.Style1x2);
			TileObjectData.newTile.Width = 3;
			TileObjectData.newTile.Height = 6;
			TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16, 16, 16, 49 };
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinatePadding = 1;
			TileObjectData.newTile.Origin = new Point16(1, 5);
			TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, 3, 0);
			TileObjectData.newTile.StyleHorizontal = true;
			TileObjectData.newTile.DrawYOffset = -12;
			TileObjectData.addTile(Type);

			AddMapEntry(new Color(150, 150, 150), CreateMapEntryName());
			DustType = DustID.WhiteTorch;

			AnimationFrameHeight = 135;
		}

		public override void AnimateTile(ref int frame, ref int frameCounter)
		{
			frameCounter++;
			if (frameCounter >= 20)
			{
				frameCounter = 0;
				frame++;
				if (frame >= 4)
				{
					frame = 0;
				}
			}
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<Items.LongDistanceZipline>();
			player.noThrow = 2;
		}

		public override void MouseOverFar(int i, int j)
		{
			Player player = Main.LocalPlayer;
			ZiplinePlayer ziplinePlayer = player.GetModPlayer<ZiplinePlayer>();

			if (ziplinePlayer.IsOnZipline)
			{
				player.cursorItemIconEnabled = true;
				player.cursorItemIconID = ModContent.ItemType<Items.LongDistanceZipline>();
				player.noThrow = 2;
			}
		}

		public override bool RightClick(int i, int j)
		{
			Player player = Main.LocalPlayer;
			ZiplinePlayer ziplinePlayer = player.GetModPlayer<ZiplinePlayer>();

			if (ziplinePlayer.IsOnZipline)
			{
				return false;
			}

			Tile tile = Main.tile[i, j];
			const int ziplineWidth = 3;
			const int ziplineHeight = 6;
			const int ziplineAnimationFrameHeight = 135;
			int frameX = (tile.TileFrameX / 17) % ziplineWidth;
			int localFrameY = tile.TileFrameY;
			if (ziplineAnimationFrameHeight > 0)
				localFrameY %= ziplineAnimationFrameHeight;
			int frameY = (localFrameY / 17) % ziplineHeight;

			Point16 originPos = new Point16(i - frameX, j - frameY);
			Point16 ziplinePos = new Point16(originPos.X + 1, originPos.Y + 5);

			ziplinePlayer.AttachToZipline(ziplinePos);
			return true;
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			Tile tile = Main.tile[i, j];
			int left = i - (tile.TileFrameX / 17) % 3;
			int top = j - (tile.TileFrameY / 17) % 6;

			if (i == left + 1 && j == top + 5)
			{
				Item.NewItem(new EntitySource_TileBreak(i, j), i * 16, j * 16, 48, 96,
					ModContent.ItemType<Items.LongDistanceZipline>(), 1);
			}
		}

		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
		{
			Tile tile = Main.tile[i, j];
			if (!tile.HasTile)
				return true;

			const int ziplineWidth = 3;
			const int ziplineHeight = 6;
			const int stride = 17;
			const int ziplineAnimationFrameHeight = 135;

			int localX = (tile.TileFrameX / stride) % ziplineWidth;
			int localFrameY = tile.TileFrameY;
			if (ziplineAnimationFrameHeight > 0)
				localFrameY %= ziplineAnimationFrameHeight;
			int localY = (localFrameY / stride) % ziplineHeight;
			if (localX != 0 || localY != 0)
				return true;

			int style = tile.TileFrameX / (ziplineWidth * stride);
			TileObjectData data = TileObjectData.GetTileData(Type, style);
			int drawYOffset = data?.DrawYOffset ?? 0;

			Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
			int animFrame = tile.TileFrameY / ziplineAnimationFrameHeight;
			int seamSrcY = animFrame * ziplineAnimationFrameHeight + ziplineHeight * stride - 2;

			Color color = Lighting.GetColor(i, j);
			Vector2 offscreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
			int originX = i;
			int originY = j;
			float destY = (originY + ziplineHeight) * 16 - (int)Main.screenPosition.Y + drawYOffset;

			for (int c = 0; c < ziplineWidth; c++)
			{
				Rectangle src = new Rectangle(style * ziplineWidth * stride + c * stride, seamSrcY, 16, 1);
				Vector2 dest = new Vector2((originX + c) * 16 - (int)Main.screenPosition.X, destY) + offscreen;
				spriteBatch.Draw(tex, dest, src, color);
			}

			return true;
		}

		public override void PostDrawPlacementPreview(int i, int j, SpriteBatch spriteBatch, Rectangle frame, Vector2 position, Color color, bool validPlacement, SpriteEffects spriteEffects)
		{
			const int ziplineWidth = 3;
			const int ziplineHeight = 6;
			const int stride = 17;
			const int ziplineAnimationFrameHeight = 135;

			int localX = (frame.X / stride) % ziplineWidth;
			int localY = ((frame.Y % ziplineAnimationFrameHeight) / stride) % ziplineHeight;
			if (localX != 0 || localY != 0)
				return;

			Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
			int styleBaseX = frame.X;
			int seamSrcY = frame.Y + ziplineHeight * stride - 2;
			float destY = position.Y + ziplineHeight * 16 + 2f;

			for (int c = 0; c < ziplineWidth; c++)
			{
				Rectangle src = new Rectangle(styleBaseX + c * stride, seamSrcY, 16, 1);
				Vector2 dest = position + new Vector2(c * 16, destY - position.Y);
				spriteBatch.Draw(tex, dest, src, color, 0f, Vector2.Zero, 1f, spriteEffects, 0f);
			}
		}
	}
}
