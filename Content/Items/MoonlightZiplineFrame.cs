using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace EndfieldZipline.Content.Items
{
	public class NightglowZiplineFrame : ModItem
	{
		public override string Texture => "EndfieldZipline/Content/Images/Item/夜明滑索架";

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
			Item.value = Item.buyPrice(0, 2, 50, 0);
			Item.rare = ItemRarityID.Yellow;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.LunarBar, 3)
				.AddTile(TileID.LunarCraftingStation)
				.Register();
		}
	}
}
