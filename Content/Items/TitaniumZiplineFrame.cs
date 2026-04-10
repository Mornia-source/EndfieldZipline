using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace EndfieldZipline.Content.Items
{
	public class TitaniumZiplineFrame : ModItem
	{
		public override string Texture => "EndfieldZipline/Content/Images/Item/钛金滑索架";

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
			Item.value = Item.buyPrice(0, 1, 0, 0);
			Item.rare = ItemRarityID.LightRed;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.TitaniumBar, 5)
				.AddTile(TileID.MythrilAnvil)
				.Register();
		}
	}
}
