using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public class CorePlayer : ModPlayer
    {
        public float critDamageBonus = 0f;

        public float independentDamageMult = 1f;
        public float targetVulnerabilityMult = 1f;

        public float projDamageMultiplier = 1f;
        public float npcDamageMultiplier = 1f;

        public override void ResetEffects()
        {
            critDamageBonus = 0f;

            independentDamageMult = 1f;
            targetVulnerabilityMult = 1f;

            projDamageMultiplier = 1f;
            npcDamageMultiplier = 1f;
        }

        public override void PostUpdateMiscEffects()
        {
            if (independentDamageMult != 1f)
                Player.GetDamage(DamageClass.Generic) *= independentDamageMult;

            if (targetVulnerabilityMult != 1f)
                Player.GetDamage(DamageClass.Generic) *= targetVulnerabilityMult;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (critDamageBonus > 0f)
                modifiers.CritDamage += critDamageBonus;
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (critDamageBonus > 0f)
                modifiers.CritDamage += critDamageBonus;
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers)
        {
            if (projDamageMultiplier != 1f)
                modifiers.FinalDamage *= projDamageMultiplier;
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers)
        {
            if (npcDamageMultiplier != 1f)
                modifiers.FinalDamage *= npcDamageMultiplier;
        }
    }
}
