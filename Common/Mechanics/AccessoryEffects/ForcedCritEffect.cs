using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    // ============================================================================
    //  ForcedCritEffect  ——  强制暴击 + 强制追踪通用模块
    // ----------------------------------------------------------------------------
    //  效果：
    //   · 接下来 ForcedCritCount 次命中直接强制暴击（不改变玩家暴击率）
    //   · 接下来 ForcedHomingCount 颗符合条件的新弹幕自带追踪（不含仆从本体）
    //   · 若命中时本该暴击（自然暴击率独立判定），额外触发 ExtraHit
    //
    //  使用示例（主动技能触发时调用）：
    //    ForcedCritEffect.Apply(player, new ForcedCritConfig
    //    {
    //        ForcedCritCount   = 6,
    //        ForcedHomingCount = 6,
    //        ExtraHit          = new ExtraHitConfig { HitDamageRatio = 0.40f, ... },
    //    });
    //
    //  状态存储在 OmniEffectsPlayer，每次命中自动消耗一次计数。
    //  追踪计数在弹幕生成时（OnSpawn）消耗，由 GlobalProjectile.ForcedHoming 处理。
    // ============================================================================

    public struct ForcedCritConfig
    {
        // ── 强制暴击 ──────────────────────────────────────────────────
        /// <summary>强制暴击的命中次数（物品命中和弹幕命中共用计数）。</summary>
        public int ForcedCritCount;

        // ── 强制追踪 ──────────────────────────────────────────────────
        /// <summary>接下来几颗符合条件的新弹幕自带追踪（仅物品直接生成，排除仆从本体、钩爪和鞭子）。</summary>
        public int ForcedHomingCount;

        // ── 本该暴击时的额外伤害 ──────────────────────────────────────
        /// <summary>
        /// 满足"本该暴击"概率时触发的额外一击。
        /// 通常使用 HitDamageRatio（触发伤害的百分比）作为伤害来源。
        /// </summary>
        public ExtraHitConfig ExtraHit;
    }

    public static class ForcedCritEffect
    {
        /// <summary>
        /// 激活强制暴击/追踪效果。多次调用以最新一次为准（覆盖前次未消耗的计数）。
        /// </summary>
        public static void Apply(Player player, in ForcedCritConfig config)
        {
            var mp = player.GetModPlayer<OmniEffectsPlayer>();
            mp.ForcedCritRemaining   = config.ForcedCritCount;
            mp.ForcedHomingRemaining = config.ForcedHomingCount;
            mp.ForcedCritExtraHit    = config.ExtraHit;
        }
    }
}
