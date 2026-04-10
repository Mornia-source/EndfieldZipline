using EndfieldZipline.Content.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace EndfieldZipline.Content.NPCs
{
	public sealed class ZiplineShopGlobalNPC : GlobalNPC
	{
		public override void ModifyShop(NPCShop shop)
		{
			if (shop.NpcType == NPCID.Merchant)
			{
				shop.Add(new Item(ModContent.ItemType<NormalZiplineFrame>())
				{
					shopCustomPrice = Item.buyPrice(silver: 60)
				});
			}
		}

		public override void SetupTravelShop(int[] shop, ref int nextSlot)
		{
			// Roughly 25% chance per travelling merchant shop generation.
			if (Main.rand.NextBool(4))
			{
				shop[nextSlot] = ModContent.ItemType<BoulderZiplineFrame>();
				nextSlot++;
			}
		}
	}
}
