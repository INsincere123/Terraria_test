using System;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Items.DamageTypes;

namespace TestMod.Common.Systems
{
    public sealed class TrueDamageSystem : ModSystem
    {
        private Hook damageHook;

        // HitModifiers 是值类型，实例方法的 this 必须通过 ref 传递。
        private delegate int GetDamageOriginal(ref NPC.HitModifiers modifiers, float baseDamage,
            bool crit, bool damageVariation, float luck);

        private delegate int GetDamageHook(GetDamageOriginal original, ref NPC.HitModifiers modifiers,
            float baseDamage, bool crit, bool damageVariation, float luck);

        public override void Load()
        {
            var method = typeof(NPC.HitModifiers).GetMethod(nameof(NPC.HitModifiers.GetDamage),
                new[] { typeof(float), typeof(bool), typeof(bool), typeof(float) });
            if (method == null)
                throw new MissingMethodException(typeof(NPC.HitModifiers).FullName, nameof(NPC.HitModifiers.GetDamage));

            damageHook = new Hook(method, (GetDamageHook)GetTrueDamage);
        }

        public override void Unload()
        {
            damageHook?.Dispose();
            damageHook = null;
        }

        private static int GetTrueDamage(GetDamageOriginal original, ref NPC.HitModifiers modifiers,
            float baseDamage, bool crit, bool damageVariation, float luck)
        {
            if (modifiers.DamageType != TrueDamageClass.Instance)
                return original(ref modifiers, baseDamage, crit, damageVariation, luck);

            // 只修改计算副本，保留原命中修正及 ToHitInfo 的后置回调。
            NPC.HitModifiers calculation = modifiers;
            StatModifier finalDamage = calculation.FinalDamage;
            float scale = finalDamage.Additive * finalDamage.Multiplicative;
            if (scale < 1f)
                finalDamage = new StatModifier(1f, 1f, finalDamage.Flat, finalDamage.Base);

            finalDamage.Base = Math.Max(0f, finalDamage.Base);
            finalDamage.Flat = Math.Max(0f, finalDamage.Flat);
            calculation.FinalDamage = finalDamage;
            if (calculation.TargetDamageMultiplier.Value < 1f)
                calculation.TargetDamageMultiplier = new();

            // SourceDamage（含 DDR）、暴击、浮动、SuperArmor 和伤害上限由原计算处理。
            return original(ref calculation, baseDamage, crit, damageVariation, luck);
        }
    }
}
