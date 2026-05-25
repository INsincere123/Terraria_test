using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Accessories
{
    // 开发者测试饰品：命中敌方时附加失明 debuff。
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

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            OnHitDebuffPlayer.AddDebuff(player, ModContent.BuffType<BlindnessDebuff>(), 300);
        }
    }
}
