using Terraria;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  TripleDodgeEffect  ——  三重闪避系统
    // ----------------------------------------------------------------------------
    //  3 种闪避机制 (vanilla 内部按顺序结算, 一次受伤至多触发一种):
    //
    //   [1] 神圣套护甲闪避 (shadowDodge)  —— 100% 闪避一次, 30 秒冷却 (vanilla 自动管理)
    //   [2] 黑带闪避       (blackBelt)    —— 10% 几率, 无冷却 (vanilla 全自动)
    //   [3] 自定义额外闪避 (FreeDodge)    —— 概率 + 冷却可调, 命中后触发忍者闪避动画
    //
    //  使用示例:
    //
    //    TripleDodgeEffect.Apply(player, new TripleDodgeConfig {
    //        EnableHallowedShadow  = true,
    //        EnableBlackBelt       = true,
    //        EnableExtra           = true,
    //        ExtraChanceDenominator = 5,    // 1/5 = 20% 几率 (设 0 禁用)
    //        ExtraCooldownTicks     = 60*15 // 15 秒冷却
    //    });
    // ============================================================================

    public struct TripleDodgeConfig
    {
        public bool EnableHallowedShadow;     // 神圣套闪避
        public bool EnableBlackBelt;          // 黑带闪避

        public bool EnableExtra;              // 自定义额外闪避
        public int  ExtraChanceDenominator;   // 几率分母 (1/N), 0 = 禁用
        public int  ExtraCooldownTicks;       // 冷却 tick
    }

    public static class TripleDodgeEffect
    {
        public static void Apply(Player player, TripleDodgeConfig cfg)
        {
            // [1] 神圣套闪避 (vanilla 自带 cooldown 检查)
            if (cfg.EnableHallowedShadow && player.shadowDodgeTimer <= 0)
                player.shadowDodge = true;

            // [2] 黑带闪避
            if (cfg.EnableBlackBelt)
                player.blackBelt = true;

            // [3] 自定义额外闪避: 写入 ModPlayer 状态, 由 FreeDodge 钩子读取
            if (cfg.EnableExtra && cfg.ExtraChanceDenominator > 0)
            {
                var mp = player.GetModPlayer<OmniEffectsPlayer>();
                mp.EnableTripleDodgeExtra        = true;
                mp.ExtraDodge_ChanceDenominator  = cfg.ExtraChanceDenominator;
                mp.ExtraDodge_CooldownTicks      = cfg.ExtraCooldownTicks;
            }
        }

        // =====================================================================
        // 由 OmniEffectsPlayer.FreeDodge 调用
        // 返回 true 表示本次伤害被闪避
        // =====================================================================
        public static bool TryExtraDodge(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnableTripleDodgeExtra) return false;
            if (mp.ExtraDodge_ChanceDenominator <= 0) return false;
            if (mp.ExtraDodgeCooldown > 0) return false;

            if (Main.rand.Next(mp.ExtraDodge_ChanceDenominator) == 0)
            {
                mp.ExtraDodgeCooldown = mp.ExtraDodge_CooldownTicks;

                // 触发原版忍者闪避动画 (黑带的视觉效果, 自带粒子)
                player.SetImmuneTimeForAllTypes(player.longInvince ? 80 : 40);
                player.NinjaDodge();
                return true;
            }

            return false;
        }
    }
}
