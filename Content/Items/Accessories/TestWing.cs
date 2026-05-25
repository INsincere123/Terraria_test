using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Content.Items.Accessories.Effects;

namespace TestMod.Content.Items.Accessories
{
    [AutoloadEquip(EquipType.Wings)]

    public class TestWing : OmniWingItem
    {
        public const float ExtraFallSpeed   = 20f;
        public const float FallGravityBoost = 1.5f;
        // ── 数值调节区 ────────────────────────────────────────────────
        protected override WingFlightConfig WingsConfig => new WingFlightConfig {
            FlyTime              = 600,   // 飞行时间
            HorizontalSpeed      = 12.8f,   // 水平速度
            HorizontalAccelMult  = 1.1f,   // 水平加速倍率
            AscentWhenFalling    = 1.2f,  // 下坠时上升力
            AscentWhenRising     = 0.15f,   // 上升中持续加速
            MaxCanAscendMult     = 0.6f,   // 持续加速阈值
            MaxAscentMult        = 4.3f,   // 最大上升速度倍率
            ConstantAscend       = 0.2f,   // 恒定上推力
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
            PerfectHoverEffect.Apply(player);
            FastFallEffect.Apply(player, ExtraFallSpeed, FallGravityBoost);
            player.AddBuff(ModContent.BuffType<GravityNormalizerBuff>(), 2);
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
