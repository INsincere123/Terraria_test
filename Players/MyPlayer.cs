using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework; // MathHelper

namespace 武器test
{
    public class MyPlayer : ModPlayer
    {
        public bool godModeBuff  = false; // 开关1
        public bool godModeBuff2 = false; // 开关2

        // 暴击伤害加成系数（由 godModeBuff2 驱动，0.5 = +50%）
        public float critDamageBonus = 0f;

        public override void ResetEffects()
        {
            // ══════════════════════════════════════════════════════════
            // Buff1：小幅强化
            // ══════════════════════════════════════════════════════════
            if (godModeBuff)
            {
                Player.statDefense                              += 3;
                Player.statLifeMax2                            += 10;
                Player.endurance                               += 0.02f;
                Player.maxMinions                              += 1;
                Player.maxTurrets                              += 1;
                Player.GetDamage(DamageClass.Generic)          += 0.03f;
                Player.GetArmorPenetration(DamageClass.Generic) += 12;
                Player.GetAttackSpeed(DamageClass.Generic)     += 0.05f;
            }

            // ══════════════════════════════════════════════════════════
            // Buff2：神模强化 + 暴击加成
            // ══════════════════════════════════════════════════════════
            if (godModeBuff2)
            {
                Player.statDefense                              += 6666;
                Player.statLifeMax2                            += 6666;
                Player.statManaMax2                            += 666;
                Player.endurance                                = MathHelper.Clamp(Player.endurance + 0.66f, 0f, 0.999f);
                Player.moveSpeed                               += 0.1f;
                Player.maxMinions                              += 21;
                Player.maxTurrets                              += 9;
                Player.GetDamage(DamageClass.Generic)          += 1.23f;
                Player.GetArmorPenetration(DamageClass.Generic) += 3600;
                Player.GetAttackSpeed(DamageClass.Generic)     += 1f;

                // 全属性暴击率 +25%
                Player.GetCritChance(DamageClass.Generic)      += 25;

                // 暴击伤害 +50%，在 ModifyHitNPC / ModifyHitNPCWithProj 里应用
                critDamageBonus = 1f;
            }
            else
            {
                // godModeBuff2 关闭时重置，防止残留
                critDamageBonus = 0f;
            }
        }

        // ██████████████████████████████████████████████████████████████
        //   ModifyHitNPC — 近战/直接命中的暴击伤害加成
        //   只在暴击时生效，非暴击命中不影响
        // ██████████████████████████████████████████████████████████████
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (critDamageBonus <= 0f) return;

            modifiers.CritDamage += critDamageBonus;
        }

        // ██████████████████████████████████████████████████████████████
        //   ModifyHitNPCWithProj — 弹射物命中的暴击伤害加成
        //   覆盖所有弹射物（矛、子弹、召唤物弹射物等）
        // ██████████████████████████████████████████████████████████████
        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (critDamageBonus <= 0f) return;

            modifiers.CritDamage += critDamageBonus;
        }
    }
}