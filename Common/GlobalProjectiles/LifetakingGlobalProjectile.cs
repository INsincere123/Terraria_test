using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.GlobalNPCs;
using TestMod.Common.Mechanics.Lifetaking;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalProjectiles
{
    public sealed class LifetakingGlobalProjectile : Terraria.ModLoader.GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        internal LifetakingLedger Ledger { get; private set; } = new();
        private bool weaponSource, channelSource;
        private bool finishing;

        public override void SetDefaults(Projectile projectile)
        { Ledger.Clear(); weaponSource = channelSource = finishing = false; }

        public override Terraria.ModLoader.GlobalProjectile Clone(Projectile from, Projectile to)
        {
            var clone = (LifetakingGlobalProjectile)base.Clone(from, to);
            clone.Ledger = Ledger.Clone();
            return clone;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (source is EntitySource_ItemUse use && use.Player.whoAmI == projectile.owner && use.Item.damage > 0)
            { weaponSource = true; channelSource = use.Item.channel; }
            else if (source is EntitySource_Parent { Entity: Projectile parent } && parent.owner == projectile.owner)
            {
                var origin = parent.GetGlobalProjectile<LifetakingGlobalProjectile>();
                // 即使新弹幕先于通用Global的标记初始化，结算时仍再次核对排除标记。
                weaponSource = origin.IsWeaponAttack(parent);
                channelSource = origin.channelSource;
            }
            else if (source is EntitySource_Parent { Entity: Player player } && player.whoAmI == projectile.owner
                && LifetakingAttackSystem.HasMeleeSource(player))
                weaponSource = true;
        }

        internal bool IsWeaponAttack(Projectile p)
        {
            if (!weaponSource || p.minion || p.sentry || p.npcProj || p.trap || p.hostile
                || ProjectileID.Sets.MinionShot[p.type] || ProjectileID.Sets.SentryShot[p.type]) return false;
            var common = p.GetGlobalProjectile<GlobalProjectile>();
            return !common.IsExtraHit && !common.IsTimeEchoAttack;
        }

        internal int Phase(Projectile p) => ProjectileID.Sets.Explosive[p.type]
            && (p.timeLeft <= 3 || p.width >= 64 && !p.tileCollide) ? 1 : 0;

        internal int Interval(Projectile p)
        {
            bool continuous = p.aiStyle == ProjAIStyleID.Flail || p.aiStyle == ProjAIStyleID.Yoyo
                || p.aiStyle == ProjAIStyleID.Drill || p.aiStyle == ProjAIStyleID.HeldProjectile
                || p.aiStyle == ProjAIStyleID.ThickLaser || p.type == ProjectileID.Electrosphere
                || p.type == ProjectileID.SolarWhipSwordExplosion || p.ownerHitCheck
                || channelSource && (Main.player[p.owner]?.heldProj == p.whoAmI
                    || p.ModProjectile?.ShouldUpdatePosition() == false);
            if (!continuous || ProjectileID.Sets.IsAWhip[p.type]) return 0;
            // 原版在Damage里直接写间隔，不能只读取默认的localNPCHitCooldown=-2。
            if (p.type == ProjectileID.LastPrismLaser) return 5;
            if (p.type == ProjectileID.Electrosphere) return 8;
            if (p.type == ProjectileID.SolarWhipSword || p.type == ProjectileID.SolarWhipSwordExplosion)
                return System.Math.Max(1, (6 + p.MaxUpdates - 1) / p.MaxUpdates);
            if (p.usesLocalNPCImmunity)
                return p.localNPCHitCooldown == -1 ? 0 : p.localNPCHitCooldown == -2 ? 10
                    : System.Math.Max(1, (p.localNPCHitCooldown + p.MaxUpdates - 1) / p.MaxUpdates);
            if (p.usesIDStaticNPCImmunity) return System.Math.Max(1, p.idStaticNPCHitCooldown);
            if (p.usesOwnerMeleeHitCD) return System.Math.Max(1, Main.player[p.owner].itemAnimationMax);
            return 10;
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if (LifetakingAttackSystem.IsVirtual(projectile))
                return LifetakingAttackSystem.EnterVirtualTarget(projectile, target) ? null : false;
            return Ledger.Blocks(target.whoAmI, target.GetGlobalNPC<HookTargetIdentity>().Generation, Phase(projectile), Main.GameUpdateCount)
                ? false : null;
        }

        public override bool CanHitPvp(Projectile projectile, Player target)
            => !LifetakingAttackSystem.IsVirtual(projectile);

        public override bool CanHitPlayer(Projectile projectile, Player target)
            => !LifetakingAttackSystem.IsVirtual(projectile);

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (damageDone > 0 && LifetakingAttackSystem.IsVirtual(projectile))
                Main.player[projectile.owner].GetModPlayer<Players.LifetakingPlayer>().Pulse();
        }

        public override bool PreKill(Projectile projectile, int timeLeft)
        {
            finishing = true;
            // 部分武器在Kill内把同一个实体改成爆炸后调用Damage；该阶段仍可正常登记。
            return true;
        }

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (finishing && Ledger.HasAttempts && projectile.owner == Main.myPlayer && Main.netMode != NetmodeID.Server)
                LifetakingAttackSystem.PlayFinish(projectile.Center);
        }
    }
}
