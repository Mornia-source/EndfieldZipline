using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace EndfieldZipline.Content.Items
{
	public class CommonZiplineFrame : ModItem
	{
		public override string Texture => "EndfieldZipline/Content/Images/Item/金属滑索架";

		public override bool CanEquipAccessory(Player player, int slot, bool modded)
		{
			return false;
		}

		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.maxStack = 1;
			Item.accessory = true;
			Item.value = Item.buyPrice(0, 0, 50, 0);
			Item.rare = ItemRarityID.White;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 10)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
