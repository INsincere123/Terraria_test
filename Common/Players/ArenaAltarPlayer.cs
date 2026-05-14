using System;
using Terraria;
using Terraria.ModLoader;
using TestMod.Buffs;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 黄泉馈灵塔玩家钩子。
    /// 职责：
    ///   · ResetEffects    — 「杀意」buff 激活时叠加伤害加成
    ///   · OnHitNPC / WithProj — 「汲命」buff 激活时对命中造成吸血治疗
    /// </summary>
    public class ArenaAltarPlayer : ModPlayer
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const float KillDamageBoostAmount = 0.10f; // 杀意：全属性伤害加算（+10%）
        public const float LifestealRate         = 0.03f; // 汲命：命中伤害的 3% 转为治疗
        public const int   LifestealHealCap      = 35;    // 汲命：单次命中回血上限（防秒杀小怪爆血）
        // ─────────────────────────────────────────────────────────────

        // ██████████████████████████████████████████████████████████████
        //   ResetEffects — 每帧调用，杀意 buff 存在时施加伤害加成
        //   Player.GetDamage 加算会影响所有伤害类型（Generic 全覆盖）
        // ██████████████████████████████████████████████████████████████
        public override void ResetEffects()
        {
            if (Player.HasBuff(ModContent.BuffType<HuangQuanDamageBoostBuff>()))
                Player.GetDamage(DamageClass.Generic) += KillDamageBoostAmount;
        }

        // ██████████████████████████████████████████████████████████████
        //   OnHitNPC — 近战 / 直接命中时的汲命结算
        // ██████████████████████████████████████████████████████████████
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            TryLifesteal(damageDone);
        }

        // ██████████████████████████████████████████████████████████████
        //   OnHitNPCWithProj — 弹射物命中时的汲命结算（覆盖召唤物、子弹等）
        // ██████████████████████████████████████████████████████████████
        public override void OnHitNPCWithProj(Projectile proj, NPC target,
            NPC.HitInfo hit, int damageDone)
        {
            TryLifesteal(damageDone);
        }

        // ── 汲命核心逻辑 ─────────────────────────────────────────────
        private void TryLifesteal(int damageDone)
        {
            if (!Player.HasBuff(ModContent.BuffType<HuangQuanLifestealBuff>())) return;

            int healAmount = (int)(damageDone * LifestealRate);
            healAmount     = Math.Min(healAmount, LifestealHealCap); // 单次上限
            if (healAmount > 0)
                Player.Heal(healAmount); // 直接治疗，绕开原版血池限制
        }
    }
}
