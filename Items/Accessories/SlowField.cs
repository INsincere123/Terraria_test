using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Rarities;

namespace TestMod.Items.Accessories
{
    /// <summary>
    /// 减速力场饰品。
    /// 以玩家为圆心，范围内所有敌人和敌方弹幕速度降至 25%。
    /// 常驻效果，无需手动触发。
    /// </summary>
    public class SlowField : ModItem
    {
        // ── 可调参数 ──────────────────────────────────────────────
        public const float Radius     = 320f;  // 力场半径（像素）
        public const float SlowFactor = 0.5f; // 速度系数（0.70 = 70% 速度）

        public override void SetDefaults()
        {
            Item.width     = 32;
            Item.height    = 32;
            Item.accessory = true;
            Item.defense = 2;
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.value     = Item.buyPrice(gold: 20);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var sfp = player.GetModPlayer<SlowFieldPlayer>();
            sfp.HasSlowField      = true;
            sfp.SlowFieldRadius   = Radius;
            sfp.SlowFactor        = SlowFactor;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 10)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
}
