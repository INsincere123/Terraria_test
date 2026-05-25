using System;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Items.DamageTypes;

namespace TestMod.Common.Players
{
    // 每帧将当前最强职业的专属加成叠加到真实伤害（Generic 已由 GetModifierInheritance 继承）
    // 伤害系统完全职业无关：不绑定近战，取四职业中加成最高者补充差值
    public class TrueDamagePlayer : ModPlayer
    {
        public override void PostUpdateEquips()
        {
            // ── 伤害加成 ─────────────────────────────────────────────────
            // GetTotalDamage 返回含所有继承链的最终加成（Additive 已包含通用部分）
            float genericAdd = Player.GetTotalDamage(DamageClass.Generic).Additive;
            float meleeAdd   = Player.GetTotalDamage(DamageClass.Melee).Additive;
            float rangedAdd  = Player.GetTotalDamage(DamageClass.Ranged).Additive;
            float magicAdd   = Player.GetTotalDamage(DamageClass.Magic).Additive;
            float summonAdd  = Player.GetTotalDamage(DamageClass.Summon).Additive;

            float bestClassAdd = Math.Max(Math.Max(meleeAdd, rangedAdd),
                                          Math.Max(magicAdd, summonAdd));

            // 去掉通用部分（已由 GetModifierInheritance 继承），只追加职业专属超出量
            float damageBonus = bestClassAdd - genericAdd;
            if (damageBonus > 0f)
                Player.GetDamage(TrueDamageClass.Instance) += damageBonus;

            // ── 暴击率加成 ───────────────────────────────────────────────
            // GetCritChance 只返回该职业专属暴击（不含继承），直接取最大值即可
            int meleeCrit  = (int)Player.GetCritChance(DamageClass.Melee);
            int rangedCrit = (int)Player.GetCritChance(DamageClass.Ranged);
            int magicCrit  = (int)Player.GetCritChance(DamageClass.Magic);
            int summonCrit = (int)Player.GetCritChance(DamageClass.Summon);

            int bestClassCrit = Math.Max(Math.Max(meleeCrit, rangedCrit),
                                         Math.Max(magicCrit, summonCrit));
            if (bestClassCrit > 0)
                Player.GetCritChance(TrueDamageClass.Instance) += bestClassCrit;
        }
    }
}
