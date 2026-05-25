using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    // ── 来源属性枚举 ────────────────────────────────────────────────────────────
    public enum SourceStatType
    {
        Defense,        // player.statDefense
        MaxHP,          // player.statLifeMax2
        CurrentHP,      // player.statLife
        MissingHP,      // statLifeMax2 - statLife
        Endurance,      // player.endurance（0~0.999，0.1 = 减伤 10%）
        LifeRegen,      // player.lifeRegen（每秒回血帧值）
        MaxMana,        // player.statManaMax2
        ShieldCurrent,  // EnergyShieldPlayer.CurrentShield
        ShieldMax,      // EnergyShieldPlayer.MaxShield
    }

    // ── 目标属性枚举 ────────────────────────────────────────────────────────────
    // 带 * 的目标在 ApplyTarget 内部自动向上取整，调用方无需关心。
    public enum TargetStatType
    {
        Damage,             // player.GetDamage(TargetClass) += value（加法增伤）
        CritChance,         // player.GetCritChance(TargetClass) += ceil(value)（整数 %）*
        ArmorPenetration,   // player.GetArmorPenetration(TargetClass) += ceil(value)（整数）*
        AttackSpeed,        // player.GetAttackSpeed(TargetClass) += value
        ManaCostReduction,  // player.manaCost *= (1 - value)，value=0.2 即省 20%
        CritDamage,         // CorePlayer.critDamageBonus += value（ModifyHitNPC 阶段）
        IndependentDamage,  // player.GetDamage(TargetClass) *= (1 + value)（独立乘区）
        TargetVulnerability,// player.GetDamage(Generic) *= (1 + value)（敌方加深乘区）
        MaxMinions,         // player.maxMinions += round(value)（召唤栏位）*
        MaxTurrets,         // player.maxTurrets += round(value)（哨兵栏位）*
    }

    // ── 曲线类型枚举 ────────────────────────────────────────────────────────────
    public enum ConversionCurve
    {
        Linear,  // t
        Power,   // t ^ CurveParam（< 1 先快后慢，> 1 先慢后快）
        Log,     // ln(1 + t*(e-1))，自然对数，先快后平，值域仍 [0,1]
        Capped,  // 线性加速，在 t = CurveParam 时到达满值（CurveParam ∈ (0,1]）
    }

    // ── 单条转换规则 ────────────────────────────────────────────────────────────
    public struct ConversionRule
    {
        // 来源
        public SourceStatType Source;
        public float SourceMin;     // 来源属性在此值时 t = 0（起始点）
        public float SourceMax;     // 来源属性在此值时 t = 1（满值点）

        // 曲线
        public ConversionCurve Curve;
        public float CurveParam;    // Power: 指数；Capped: 满值比例（0~1）

        // 输出
        public float OutputScale;   // t = 1 时的最大输出量
        public float OutputMax;     // 额外上限，0 = 不限制

        // 目标
        public TargetStatType Target;
        public DamageClass TargetClass; // null 时退化为 DamageClass.Generic
    }

    // ============================================================================
    //  StatConversionPlayer  ——  生存属性→伤害属性转换系统核心
    // ----------------------------------------------------------------------------
    //  调用方式（饰品 UpdateAccessory 里）：
    //    StatConversionPlayer.AddRule(player, new ConversionRule { ... });
    //
    //  可添加任意条规则，同一来源或同一目标可多次使用。
    //  所有规则在 PostUpdateMiscEffects 统一计算并写入，确保来源属性已最终确定。
    //
    //  时序说明：
    //    CritDamage        → 写 CorePlayer.critDamageBonus，在 ModifyHitNPC 读取，无时序问题
    //    IndependentDamage → 直接写 GetDamage() *= (1+x)，与 CorePlayer 无时序依赖
    //    TargetVulnerability → 同上
    // ============================================================================
    public class StatConversionPlayer : ModPlayer
    {
        private readonly List<ConversionRule> _rules = new();

        /// <summary>注册一条转换规则，在饰品的 UpdateAccessory 里调用。</summary>
        public static void AddRule(Player player, ConversionRule rule)
            => player.GetModPlayer<StatConversionPlayer>()._rules.Add(rule);

        public override void ResetEffects() => _rules.Clear();

        public override void PostUpdateMiscEffects()
        {
            if (_rules.Count == 0) return;

            // 先快照所有来源属性值，再统一写入目标。
            // 防止某条规则写入的属性被同帧后续规则当来源读取，
            // 避免"伤害→防御→伤害→…"的帧内循环放大。
            var outputs = new float[_rules.Count];
            for (int i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                outputs[i] = ComputeOutput(Player, in rule);
            }

            for (int i = 0; i < _rules.Count; i++)
            {
                if (outputs[i] == 0f) continue;
                var rule = _rules[i];
                ApplyTarget(Player, in rule, outputs[i]);
            }
        }

        // ── 计算 ──────────────────────────────────────────────────────────────
        private static float ComputeOutput(Player player, in ConversionRule rule)
        {
            float raw   = GetSourceValue(player, rule.Source);
            float range = rule.SourceMax - rule.SourceMin;
            if (range <= 0f) return 0f;

            float t = Math.Clamp((raw - rule.SourceMin) / range, 0f, 1f);

            float curved = rule.Curve switch
            {
                ConversionCurve.Linear => t,
                ConversionCurve.Power  => MathF.Pow(t, rule.CurveParam),
                ConversionCurve.Log    => MathF.Log(1f + t * (MathF.E - 1f)),
                ConversionCurve.Capped => Math.Min(t / MathF.Max(rule.CurveParam, 0.001f), 1f),
                _                      => t,
            };

            float output = curved * rule.OutputScale;
            return rule.OutputMax > 0f ? Math.Min(output, rule.OutputMax) : output;
        }

        private static float GetSourceValue(Player player, SourceStatType source) => source switch
        {
            SourceStatType.Defense       => player.statDefense,
            SourceStatType.MaxHP         => player.statLifeMax2,
            SourceStatType.CurrentHP     => player.statLife,
            SourceStatType.MissingHP     => player.statLifeMax2 - player.statLife,
            SourceStatType.Endurance     => player.endurance,
            SourceStatType.LifeRegen     => player.lifeRegen,
            SourceStatType.MaxMana       => player.statManaMax2,
            SourceStatType.ShieldCurrent => player.GetModPlayer<EnergyShieldPlayer>().CurrentShield,
            SourceStatType.ShieldMax     => player.GetModPlayer<EnergyShieldPlayer>().MaxShield,
            _                            => 0f,
        };

        private static void ApplyTarget(Player player, in ConversionRule rule, float output)
        {
            DamageClass cls = rule.TargetClass ?? DamageClass.Generic;
            switch (rule.Target)
            {
                case TargetStatType.Damage:
                    player.GetDamage(cls) += output;
                    break;
                case TargetStatType.CritChance:
                    player.GetCritChance(cls) += MathF.Ceiling(output);
                    break;
                case TargetStatType.ArmorPenetration:
                    player.GetArmorPenetration(cls) += MathF.Ceiling(output);
                    break;
                case TargetStatType.AttackSpeed:
                    player.GetAttackSpeed(cls) += output;
                    break;
                case TargetStatType.ManaCostReduction:
                    player.manaCost *= 1f - output;
                    break;
                case TargetStatType.CritDamage:
                    player.GetModPlayer<CorePlayer>().critDamageBonus += output;
                    break;
                case TargetStatType.IndependentDamage:
                    player.GetDamage(cls) *= 1f + output;
                    break;
                case TargetStatType.TargetVulnerability:
                    player.GetDamage(DamageClass.Generic) *= 1f + output;
                    break;
                case TargetStatType.MaxMinions:
                    player.maxMinions += (int)MathF.Round(output);
                    break;
                case TargetStatType.MaxTurrets:
                    player.maxTurrets += (int)MathF.Round(output);
                    break;
            }
        }
    }
}
