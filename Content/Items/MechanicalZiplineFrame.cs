using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace EndfieldZipline.Content.Items
{
	public class MechanicalZiplineFrame : ModItem
	{
		public override string Texture => "EndfieldZipline/Content/Images/Item/机械滑索架";

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
			Item.value = Item.buyPrice(0, 1, 50, 0);
			Item.rare = ItemRarityID.Pink;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(549, 1)
				.AddIngredient(547, 1)
				.AddIngredient(548, 1)
				.AddTile(TileID.MythrilAnvil)
				.Register();
		}
	}
}
