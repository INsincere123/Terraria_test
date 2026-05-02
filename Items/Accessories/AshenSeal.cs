using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Rarities;

namespace TestMod.Items.Accessories
{
    /// <summary>
    /// 示例饰品：穿上即触发超级输出
    /// 防御每点 → +1% 全伤害，防御清零（不影响免伤）
    /// </summary>
    public class AshenSeal : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.value = Item.sellPrice(gold: 10);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<SuperOutputPlayer>().SuperOutputActive = true;
        }

    }
}
