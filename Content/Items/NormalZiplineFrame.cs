using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace EndfieldZipline.Content.Items
{
	public class NormalZiplineFrame : ModItem
	{
		public override string Texture => "EndfieldZipline/Content/Images/Item/普通滑索架";

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
			Item.value = Item.buyPrice(0, 0, 60, 0);
			Item.rare = ItemRarityID.White;
		}
	}
}
