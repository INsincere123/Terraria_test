using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            // 困难模式的血月期间，高尔夫球手出售三件终阶工具/饰品。
            if (shop.NpcType == NPCID.Golfer)
            {
                foreach (int type in new[] { ItemID.Shellphone, ItemID.TerrasparkBoots, ItemID.AnkhShield })
                {
                    shop.Add(new Item(type)
                    {
                        shopCustomPrice = Item.buyPrice(platinum: 3),
                    }, Condition.Hardmode, Condition.BloodMoon);
                }
            }

            // 血月期间，机械师出售专注宝珠。
            if (shop.NpcType == NPCID.Mechanic)
            {
                shop.Add(new Item(ModContent.ItemType<FocusOrb>())
                {
                    shopCustomPrice = Item.buyPrice(platinum: 1),
                }, Condition.BloodMoon);
            }

            // 公主常驻出售天赐华冠。
            if (shop.NpcType == NPCID.Princess)
            {
                shop.Add(new Item(ModContent.ItemType<HeavenlyCrown>())
                {
                    shopCustomPrice = Item.buyPrice(platinum: 3),
                });
            }
        }
    }
}
