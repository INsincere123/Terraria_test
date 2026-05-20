using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public class SummonCritPlayer : ModPlayer
    {
        public bool Enabled;

        public static void Enable(Player player)
            => player.GetModPlayer<SummonCritPlayer>().Enabled = true;

        public override void ResetEffects()
        {
            Enabled = false;
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            TryApplySummonCrit(Player, proj, ref modifiers, requireEnabled: true);
        }

        public static void TryApplySummonCrit(Player player, Projectile proj, ref NPC.HitModifiers modifiers, bool requireEnabled = true)
        {
            if (requireEnabled && !player.GetModPlayer<SummonCritPlayer>().Enabled)
                return;

            if (proj.hostile || !IsSummonDamage(proj))
                return;

            float critChance = player.GetTotalCritChance(DamageClass.Generic);
            if (critChance > 0f && Main.rand.NextFloat(100f) < critChance)
                modifiers.SetCrit();
        }

        public static bool IsSummonDamage(Projectile projectile)
        {
            return projectile.CountsAsClass(DamageClass.Summon)
                || projectile.CountsAsClass(DamageClass.SummonMeleeSpeed)
                || projectile.minion
                || projectile.sentry
                || projectile.minionSlots > 0f
                || ProjectileID.Sets.MinionSacrificable[projectile.type]
                || ProjectileID.Sets.MinionShot[projectile.type]
                || ProjectileID.Sets.SentryShot[projectile.type]
                || ProjectileID.Sets.IsAWhip[projectile.type];
        }
    }
}
