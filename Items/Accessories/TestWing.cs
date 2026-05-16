using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Items.Accessories.Effects;

namespace TestMod.Items.Accessories
{
    [AutoloadEquip(EquipType.Wings)]

    public class TestWing : OmniWingItem
    {
        public const float ExtraFallSpeed   = 20f;
        public const float FallGravityBoost = 1.5f;
        // ── 数值调节区 ────────────────────────────────────────────────
        protected override WingFlightConfig WingsConfig => new WingFlightConfig {
            FlyTime              = 600,   // 飞行时间 tick（30 秒）
            HorizontalSpeed      = 11.5f,   // 水平速度（接近女皇之翼）
            HorizontalAccelMult  = 1.5f,   // 水平加速倍率
            AscentWhenFalling    = 0.85f,  // 下坠时上升力
            AscentWhenRising     = 0.1f,   // 上升中持续加速
            MaxCanAscendMult     = 0.5f,   // 持续加速阈值
            MaxAscentMult        = 1.5f,   // 最大上升速度倍率
            ConstantAscend       = 0.1f,   // 恒定上推力
            // EnableHover = false（默认），HoverHorizontalSpeed / HoverAccelMult 无需填写
            // UpBoostMultiplier 触发条件 > 1f，默认 0 = 不启用，可省略
            // InfiniteFlight / EnablePerfectHover 为 bool，默认 false，可省略
        };
        // ─────────────────────────────────────────────────────────────

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            base.UpdateAccessory(player, hideVisual);

            MoveSpeedEffect.Apply(player, new MoveSpeedConfig {
                RunSpeedCap = 9f,   // 火神靴级别奔跑速度上限
            });
            
            FastFallEffect.Apply(player, ExtraFallSpeed, FallGravityBoost);
        }

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.rare  = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 5);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.FeatherfallPotion)
                .Register();
        }
    }
}
