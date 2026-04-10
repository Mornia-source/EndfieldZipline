using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace EndfieldZipline.Content.Items
{
	public class LongDistanceZipline : ModItem
	{
		public override string Texture => "EndfieldZipline/Content/Images/Item/长距离滑索";

		public override void SetDefaults()
		{
			Item.width = 50;
			Item.height = 112;
			Item.maxStack = 999;
			Item.useTurn = true;
			Item.autoReuse = true;
			Item.useAnimation = 15;
			Item.useTime = 10;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.consumable = true;
			Item.value = Item.buyPrice(silver: 10);
			Item.rare = ItemRarityID.Blue;

			Item.createTile = ModContent.TileType<Tiles.LongDistanceZiplineTile>();
			Item.placeStyle = 0;
		}

		public override void AddRecipes()
		{
			CreateRecipe(2)
				.AddIngredient(ItemID.Rope, 50)
				.AddIngredient(ItemID.Chain, 10)
				.AddRecipeGroup(RecipeGroupID.IronBar, 5)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
