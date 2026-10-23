using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC
    {
        // 共享 GlobalNPC 不允许实例字段；警告去重属于本轮模组加载。
        private static bool outlandShopOverflowWarned;

        public override void Unload() => outlandShopOverflowWarned = false;

        public override void ModifyActiveShop(NPC npc, string shopName, Item[] items)
        {
            if (npc.type != NPCID.TravellingMerchant) return;
            AddOutlandBook(new Item(ModContent.ItemType<OutlandCombatTechniquesVolumeOne>()), items);
            if (NPC.downedMoonlord)
                AddOutlandBook(new Item(ModContent.ItemType<OutlandCombatTechniquesVolumeTwo>()), items);
        }

        private void AddOutlandBook(Item book, Item[] items)
        {
            int limit = System.Math.Min(39, items.Length - 1);
            for (int i = 0; i < limit; i++)
                if (items[i]?.type == book.type) return;
            for (int i = 0; i < limit; i++)
            {
                // null 是未占用格；IsAir 可能是条件不满足的预留格，不能覆盖。
                if (items[i] != null) continue;
                book.shopCustomPrice = ((OutlandCombatTechniquesItem)book.ModItem).Price;
                items[i] = book;
                return;
            }
            if (!outlandShopOverflowWarned)
            {
                Mod.Logger.Warn("旅商商店已满，跳过外域战斗技术；保留原有商品和回购格。");
                outlandShopOverflowWarned = true;
            }
        }

        public override void ModifyShop(NPCShop shop)
        {
            // 满月期间，巫医出售两件御灵饰品。
            if (shop.NpcType == NPCID.WitchDoctor)
            {
                foreach (int type in new[] { ModContent.ItemType<SpiritGuidanceCore>(), ModContent.ItemType<SummonOverdriveCore>() })
                {
                    shop.Add(new Item(type)
                    {
                        shopCustomPrice = Item.buyPrice(platinum: 1),
                    }, Condition.MoonPhaseFull);
                }
            }

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
