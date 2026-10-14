using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Items.Accessories
{
    // 保留旧物品身份；诱敌功能已迁移到独立道具。
    // 无合成配方，/item DevTestAccessory 获取。
    public class DevTestAccessory : ModItem
    {
        public override void SetDefaults()
        {
            Item.width     = 24;
            Item.height    = 24;
            Item.accessory = true;
            Item.rare      = ItemRarityID.Red;
            Item.value     = 0;
        }

    }
}
