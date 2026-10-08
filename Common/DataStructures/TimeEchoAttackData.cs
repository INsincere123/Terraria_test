using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TestMod.Common.DataStructures
{
    internal readonly record struct TimeEchoAttackIdentity(uint Root, uint Generation);

    internal enum TimeEchoShotCoordinates : byte { MuzzleRelative, WorldTarget }

    internal sealed class TimeEchoAttackSource : IEntitySource
    {
        public string Context => "TimeEchoAttack";
        internal TimeEchoAttackIdentity Identity { get; }
        internal DamageClass DamageClass { get; }
        internal int CritChance { get; }
        internal int ArmorPenetration { get; }
        internal TimeEchoShot? Shot { get; }
        internal Vector2? OwnerOrigin { get; }
        internal TimeEchoAttackSource(TimeEchoAttackIdentity identity, DamageClass damageClass, int crit, int penetration,
            TimeEchoShot? shot = null, Vector2? ownerOrigin = null)
        { Identity = identity; DamageClass = damageClass; CritChance = crit; ArmorPenetration = penetration; Shot = shot; OwnerOrigin = ownerOrigin; }
    }

    internal readonly record struct TimeEchoShot(Projectile Entity, int Identity, int Type, Vector2 Center,
        Vector2 Velocity, Vector2 PlayerCenter, Vector2 Mouse, Vector2 Aim, int Damage, int OriginalDamage,
        float Knockback, DamageClass DamageClass, int Crit, int ArmorPenetration, float Ai0, float Ai1, float Ai2,
        float Scale, int Penetrate, int MaxPenetrate, int TimeLeft, int ExtraUpdates, bool TileCollide,
        bool IgnoreWater, bool LocalImmunity, int HitCooldown, uint Root, uint Generation, ulong Tick,
        TimeEchoOwnerPose OwnerPose = default, TimeEchoShotCoordinates Coordinates = TimeEchoShotCoordinates.MuzzleRelative,
        bool PreserveBubbleAmmo = false, float BubbleAmmo = 0, float BubbleSpeed = 0)
    {
        internal void ApplyParameters(Projectile p)
        {
            p.originalDamage = OriginalDamage;
            p.scale = Scale; p.penetrate = Penetrate; p.maxPenetrate = MaxPenetrate;
            p.timeLeft = TimeLeft; p.extraUpdates = ExtraUpdates;
            p.tileCollide = TileCollide; p.ignoreWater = IgnoreWater;
            p.CritChance = Crit; p.ArmorPenetration = ArmorPenetration;
            p.usesLocalNPCImmunity = LocalImmunity;
            p.localNPCHitCooldown = HitCooldown;
            if (PreserveBubbleAmmo && p.type == Terraria.ID.ProjectileID.Xenopopper)
            { p.localAI[0] = BubbleAmmo; p.localAI[1] = BubbleSpeed; }
        }
        internal static TimeEchoShot Capture(Projectile p, Player player, Vector2 center, Vector2 mouse,
            Vector2 aim, uint root, uint generation) => new(p, p.identity, p.type, p.Center, p.velocity,
                center, mouse, aim, p.damage, p.originalDamage, p.knockBack, p.DamageType, p.CritChance,
                p.ArmorPenetration, p.ai[0], p.ai[1], p.ai[2], p.scale, p.penetrate, p.maxPenetrate,
                p.timeLeft, p.extraUpdates, p.tileCollide, p.ignoreWater, p.usesLocalNPCImmunity,
                p.localNPCHitCooldown, root, generation, Main.GameUpdateCount,
                TimeEchoOwnerPose.NeedsProxy(p) ? TimeEchoOwnerPose.Capture(player, p) : default,
                p.GetGlobalProjectile<Common.GlobalProjectiles.GlobalProjectile>().EchoSpawnCoordinates ??
                    Common.Systems.TimeEchoAttackSystem.GetShotCoordinates(p),
                p.GetGlobalProjectile<Common.GlobalProjectiles.GlobalProjectile>().EchoPreserveBubbleAmmo,
                p.localAI[0], p.localAI[1]);
    }

    internal readonly record struct TimeEchoOwnerPose(int ItemType, int Animation, int Direction, float Gravity,
        float WhipRange, Rectangle BodyFrame, float Rotation, Vector2 RotationOrigin, int WhipSegments = 0, float WhipBaseRange = 0, int AnimationLeft = 0, float InverseMeleeSpeed = 1)
    {
        internal static bool NeedsProxy(Projectile p) => Common.Systems.TimeEchoAttackSystem.UsesEchoOwner(p) || p.ownerHitCheck || Terraria.ID.ProjectileID.Sets.IsAWhip[p.type] ||
            p.aiStyle == Terraria.ID.ProjAIStyleID.NightsEdge || p.aiStyle == Terraria.ID.ProjAIStyleID.TrueNightsEdge ||
            p.aiStyle == Terraria.ID.ProjAIStyleID.StellarTune;
        internal static TimeEchoOwnerPose Capture(Player player, Projectile projectile) => new(player.HeldItem.type,
            Math.Max(1, player.itemAnimationMax), player.direction, player.gravDir, player.whipRangeMultiplier,
            player.bodyFrame, player.fullRotation, player.fullRotationOrigin,
            projectile.WhipSettings.Segments, projectile.WhipSettings.RangeMultiplier,
            player.itemAnimation, 1f / player.GetTotalAttackSpeed(DamageClass.Melee));
    }

    internal readonly record struct TimeEchoMeleeFrame(Rectangle Hitbox, Vector2 PlayerCenter, Vector2 Mouse,
        int Damage, int Crit, float Knockback, int ArmorPenetration, DamageClass DamageClass, int ItemType,
        Vector2 GripOffset, float Scale, float Rotation, int Direction, float Gravity, Rectangle BodyFrame,
        Player.CompositeArmData FrontArm, Player.CompositeArmData BackArm, uint Swing, ulong Tick);
}
