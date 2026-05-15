using System;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Items.Accessories.Effects
{
    // ============================================================================
    //  LifestealEffect  ——  吸血/生命汲取通用模块
    // ----------------------------------------------------------------------------
    //  使用示例（饰品）:
    //    LifestealEffect.Apply(player, LifestealConfig.Default with {
    //        HitDamageRatio = 0.05f,   // 命中伤害的 5% 转为治疗
    //        HealCap        = 50,       // 单次最多回 50
    //    });
    //
    //  使用示例（仅近战、暴击才触发、有冷却）:
    //    LifestealEffect.Apply(player, LifestealConfig.Default with {
    //        HitDamageRatio     = 0.08f,
    //        DamageClassFilter  = DamageClass.Melee,
    //        CritOnly           = true,
    //        CooldownTicks      = 60,
    //    });
    //
    //  注意:
    //   - Apply() 应在 UpdateAccessory / PostUpdateMiscEffects 中每帧调用
    //   - buff 触发型效果应在 PostUpdateMiscEffects 而非 ResetEffects 中调用
    //     （避免 ModPlayer 执行顺序导致 EnableLifesteal 被后续 ResetEffects 重置）
    //   - LifeRegen 类型：治疗量转为回血队列，由 UpdateLifeRegen 平滑释放
    //   - Shield 类型：预留接口，当前回退为即时治疗（待接入能量护盾系统）
    // ============================================================================

    public enum LifestealHealType
    {
        Instant,    // 直接 Player.Heal()
        LifeRegen,  // 转为 lifeRegen 队列，平滑释放
        Shield,     // 回能量护盾（预留，当前回退为 Instant）
    }

    public struct LifestealConfig
    {
        // ── 回血量公式（四路叠加，最终取整）──────────────────────────────
        public float HitDamageRatio;   // 触发伤害   × 比例（0.03 = 3%）
        public int   FlatHeal;         // 固定回血量（与伤害无关）
        public float MaxHpRatio;       // 最大生命   × 比例
        public float MissingHpRatio;   // 当前缺血量 × 比例（低血时更多）

        // ── 回血上限（两者同时生效时取较小值）──────────────────────────
        public int   HealCap;       // 单次回血上限（0 = 不限）
        public float HealCapRatio;  // 单次上限为最大生命的 X%（0 = 不用）

        // ── 来源过滤 ──────────────────────────────────────────────────
        public bool        TriggerOnItem;       // 物品直接命中是否触发
        public bool        TriggerOnProj;       // 弹幕命中是否触发
        public DamageClass DamageClassFilter;   // null = 不过滤伤害类型

        // ── 触发条件 ──────────────────────────────────────────────────
        public float ProcChance;    // 触发概率（0~1，1 = 必定触发）
        public bool  CritOnly;      // 仅暴击时触发
        public float HpThreshold;   // 仅 HP 低于此比例时触发（0 = 不限）
        public int   CooldownTicks; // 两次触发之间的冷却帧数（0 = 无冷却）

        // ── 回血方式 ──────────────────────────────────────────────────
        public LifestealHealType HealType;

        // 默认值：物品/弹幕都触发，无附加条件，必定触发，即时回血
        public static LifestealConfig Default => new()
        {
            TriggerOnItem = true,
            TriggerOnProj = true,
            ProcChance    = 1f,
            HealType      = LifestealHealType.Instant,
        };
    }

    public static class LifestealEffect
    {
        // 饰品在 UpdateAccessory 中调用，buff 效果在 PostUpdateMiscEffects 中调用
        public static void Apply(Player player, LifestealConfig config)
        {
            var mp = player.GetModPlayer<OmniEffectsPlayer>();
            mp.EnableLifesteal = true;
            mp.LifestealConfig = config;
        }

        // 由 OmniEffectsPlayer.OnHitNPCWithItem / OnHitNPCWithProj 调用
        public static void TryHeal(Player player, OmniEffectsPlayer mp,
            NPC.HitInfo hit, int damageDone, bool fromProjectile)
        {
            if (!mp.EnableLifesteal) return;

            LifestealConfig cfg = mp.LifestealConfig;

            // ── 来源过滤 ──────────────────────────────────────────────
            if ( fromProjectile && !cfg.TriggerOnProj) return;
            if (!fromProjectile && !cfg.TriggerOnItem) return;

            // ── 伤害类型过滤 ──────────────────────────────────────────
            if (cfg.DamageClassFilter != null &&
                !hit.DamageType.CountsAsClass(cfg.DamageClassFilter)) return;

            // ── 暴击条件 ──────────────────────────────────────────────
            if (cfg.CritOnly && !hit.Crit) return;

            // ── HP 阈值 ───────────────────────────────────────────────
            if (cfg.HpThreshold > 0f &&
                (float)player.statLife / player.statLifeMax2 > cfg.HpThreshold) return;

            // ── 冷却 ──────────────────────────────────────────────────
            if (mp.LifestealCooldown > 0) return;

            // ── 触发概率 ──────────────────────────────────────────────
            if (cfg.ProcChance < 1f && Main.rand.NextFloat() >= cfg.ProcChance) return;

            // ── 计算并发放治疗 ────────────────────────────────────────
            int heal = ComputeHeal(player, cfg, damageDone);
            if (heal <= 0) return;

            DeliverHeal(player, mp, cfg, heal);

            if (cfg.CooldownTicks > 0)
                mp.LifestealCooldown = cfg.CooldownTicks;
        }

        // 由 OmniEffectsPlayer.UpdateLifeRegen 调用，平滑释放 LifeRegen 队列
        public static void DrainPendingRegen(Player player, OmniEffectsPlayer mp)
        {
            if (mp.LifestealPendingRegen <= 0) return;

            // 以约 5 HP/s 速率释放（vanilla 单位：2 = 1 HP/s，10 单位 = 5 HP/s）
            int drain = Math.Min(mp.LifestealPendingRegen, 10);
            player.lifeRegen          += drain;
            mp.LifestealPendingRegen  -= drain;
        }

        // ── 内部：计算治疗量 ──────────────────────────────────────────────────

        private static int ComputeHeal(Player player, in LifestealConfig cfg, int damageDone)
        {
            float raw = 0f;
            raw += damageDone * cfg.HitDamageRatio;
            raw += cfg.FlatHeal;
            raw += player.statLifeMax2 * cfg.MaxHpRatio;
            raw += (player.statLifeMax2 - player.statLife) * cfg.MissingHpRatio;

            int heal = (int)raw;

            if (cfg.HealCap > 0)
                heal = Math.Min(heal, cfg.HealCap);
            if (cfg.HealCapRatio > 0f)
                heal = Math.Min(heal, (int)(player.statLifeMax2 * cfg.HealCapRatio));

            return Math.Max(0, heal);
        }

        // ── 内部：发放治疗 ────────────────────────────────────────────────────

        private static void DeliverHeal(Player player, OmniEffectsPlayer mp,
            in LifestealConfig cfg, int amount)
        {
            switch (cfg.HealType)
            {
                case LifestealHealType.Instant:
                    player.Heal(amount);
                    break;

                case LifestealHealType.LifeRegen:
                    // 加入队列，由 DrainPendingRegen 每帧平滑释放
                    // ×2 将 HP 转换为 vanilla lifeRegen 单位（1 HP = 2 单位/s）
                    mp.LifestealPendingRegen += amount * 2;
                    break;

                case LifestealHealType.Shield:
                    // 预留：接入能量护盾系统
                    // EnergyShieldEffect.AddShield(player, amount);
                    player.Heal(amount); // 暂时回退为即时治疗
                    break;
            }
        }
    }
}
