using System;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Items.DamageTypes;

namespace TestMod.Common.Players
{
    // 每帧将当前最强职业的专属加成叠加到真实伤害（Generic 已由 GetModifierInheritance 继承）
    // 以手持武器基础伤害比较四职业的完整结算，合并获胜职业的专属修正。
    public class TrueDamagePlayer : ModPlayer
    {
        public override void PreUpdateMovement()
        {
            // 此时原版潜行、魔力病和所有 PostUpdateMiscEffects 已结算，且尚未执行物品攻击。
            // ── 伤害加成 ─────────────────────────────────────────────────
            // Base/Flat 会使职业排名随基础伤害变化；空手时按 1 点基础伤害比较。
            float comparisonDamage = Math.Max(1, Player.HeldItem.damage);
            StatModifier genericDamage = Player.GetTotalDamage(DamageClass.Generic);
            StatModifier bestClassDamage = StatModifier.Default;
            float bestDamage = genericDamage.ApplyTo(comparisonDamage);

            SelectClassDamage(DamageClass.Melee, genericDamage, comparisonDamage, ref bestClassDamage, ref bestDamage);
            SelectClassDamage(DamageClass.Ranged, genericDamage, comparisonDamage, ref bestClassDamage, ref bestDamage);
            SelectClassDamage(DamageClass.Magic, genericDamage, comparisonDamage, ref bestClassDamage, ref bestDamage);
            SelectClassDamage(DamageClass.Summon, genericDamage, comparisonDamage, ref bestClassDamage, ref bestDamage);

            // 四个原版职业的数值仅继承 Generic。只合并职业自身的修正，完整保留
            // Additive/Multiplicative/Base/Flat，Generic 仍由真实伤害类型继承一次。
            ref StatModifier trueDamage = ref Player.GetDamage(TrueDamageClass.Instance);
            trueDamage = trueDamage.CombineWith(bestClassDamage);

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

        private void SelectClassDamage(DamageClass damageClass, StatModifier genericDamage, float comparisonDamage,
            ref StatModifier bestClassDamage, ref float bestDamage)
        {
            StatModifier classDamage = Player.GetDamage(damageClass);
            float damage = genericDamage.CombineWith(classDamage).ApplyTo(comparisonDamage);
            if (damage <= bestDamage)
                return;

            bestClassDamage = classDamage;
            bestDamage = damage;
        }
    }
}
