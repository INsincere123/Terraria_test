using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Content.Items.Accessories.Effects;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 黄泉馈灵塔玩家钩子。
    /// 职责：
    ///   · ResetEffects        — 「杀意」buff 激活时叠加伤害加成
    ///   · PostUpdateMiscEffects — 「汲命」buff 激活时注册吸血模块
    ///     （在此而非 ResetEffects 中注册，避免 ModPlayer 执行顺序导致 EnableLifesteal 被重置）
    /// </summary>
    public class ArenaAltarPlayer : ModPlayer
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const float KillDamageBoostAmount = 0.10f; // 杀意：全属性伤害加算（+10%）
        public const float LifestealRate         = 0.03f; // 汲命：命中伤害的 3% 转为治疗
        public const int   LifestealHealCap      = 35;    // 汲命：单次命中回血上限（防秒杀小怪爆血）
        // ─────────────────────────────────────────────────────────────

        private static readonly LifestealConfig HuangQuanLifestealConfig = LifestealConfig.Default with
        {
            HitDamageRatio = LifestealRate,
            HealCap        = LifestealHealCap,
        };

        // ██████████████████████████████████████████████████████████████
        //   ResetEffects — 每帧调用，杀意 buff 存在时施加伤害加成
        // ██████████████████████████████████████████████████████████████
        public override void ResetEffects()
        {
            if (Player.HasBuff(ModContent.BuffType<HuangQuanDamageBoostBuff>()))
                Player.GetDamage(DamageClass.Generic) += KillDamageBoostAmount;
        }

        // ██████████████████████████████████████████████████████████████
        //   PostUpdateMiscEffects — 汲命 buff 存在时注册吸血效果
        //   OnHitNPCWithItem / OnHitNPCWithProj 分发由 OmniEffectsPlayer 统一处理
        // ██████████████████████████████████████████████████████████████
        public override void PostUpdateMiscEffects()
        {
            if (Player.HasBuff(ModContent.BuffType<HuangQuanLifestealBuff>()))
                LifestealEffect.Apply(Player, HuangQuanLifestealConfig);
        }
    }
}
