using Terraria;
using Terraria.ModLoader;

namespace TestMod.Items.Accessories.Effects
{
    // ============================================================================
    //  ForcedCritEffect  ——  强制暴击 + 强制追踪通用模块
    // ----------------------------------------------------------------------------
    //  效果：
    //   · 接下来 ForcedCritCount 次命中必定暴击（对所有伤害类型施加 +CritBoost 暴击率）
    //   · 接下来 ForcedHomingCount 次弹幕发射自带追踪（按弹幕生成次数计）
    //   · 若命中时本该暴击（自然暴击率独立判定），额外触发 ExtraHit
    //
    //  使用示例（主动技能触发时调用）：
    //    ForcedCritEffect.Apply(player, new ForcedCritConfig
    //    {
    //        ForcedCritCount   = 6,
    //        ForcedHomingCount = 6,
    //        CritBoost         = 100f,
    //        ExtraHit          = new ExtraHitConfig { HitDamageRatio = 0.40f, ... },
    //    });
    //
    //  状态存储在 OmniEffectsPlayer，每次命中自动消耗一次计数。
    //  追踪计数在弹幕生成时（OnSpawn）消耗，由 GlobalProjectile.ForcedHoming 处理。
    // ============================================================================

    public struct ForcedCritConfig
    {
        // ── 强制暴击 ──────────────────────────────────────────────────
        /// <summary>强制暴击的命中次数（物品命中和弹幕命中各自独立计数）。</summary>
        public int ForcedCritCount;

        /// <summary>
        /// 施加给玩家的额外暴击率（用于保证必定暴击）。
        /// 计算自然暴击率时会减去此值：naturalCrit = GetCritChance() - CritBoost。
        /// 推荐设置为 100，对所有普通装备配置均可保证暴击。
        /// </summary>
        public float CritBoost;

        // ── 强制追踪 ──────────────────────────────────────────────────
        /// <summary>接下来几次弹幕发射自带追踪（按生成次数计，仅物品直接生成的弹幕）。</summary>
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
            mp.ForcedCritBoost       = config.CritBoost;
            mp.ForcedCritExtraHit    = config.ExtraHit;
        }
    }
}
