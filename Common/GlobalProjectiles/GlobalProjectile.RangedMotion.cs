using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Content.Prefixes;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        private bool rangedMotionSource;
        private bool rangedMotionRefined;
        // 1 表示当前速度自然；2/4 表示速度已被放大。同步时必须连同这一阶段发送。
        private byte rangedMotionApplied = 1;
        private Projectile rangedMotionPendingParent;
        private int rangedMotionParentIdentity, rangedMotionParentType;
        private bool rangedMotionRocketAtSpawn;

        private void RangedMotion_OnSpawn(Projectile p, IEntitySource source)
        {
            if (p.owner != Main.myPlayer || p.owner < 0 || p.owner >= Main.maxPlayers ||
                p.hostile || p.npcProj || IsExtraHit || IsTimeEchoAttack) return;
            if (source is EntitySource_ItemUse use && use.Player.whoAmI == p.owner)
            {
                rangedMotionSource = true;
                rangedMotionRefined = use.Item.CountsAsClass(DamageClass.Ranged) &&
                    PrefixLoader.GetPrefix(use.Item.prefix) is RefinementRangedPrefix or RefinementRangedNoKBPrefix;
            }
            else if (source is EntitySource_Parent { Entity: Projectile parent } &&
                source is not IEntitySource_OnHit && parent.owner == p.owner)
            {
                var data = parent.GetGlobalProjectile<GlobalProjectile>();
                if (!data.IsExtraHit && !data.IsTimeEchoAttack)
                {
                    // 只有发射载体传递词条；分裂、命中和死亡派生不继承运动倍率。
                    if (TimeEchoAttackSystem.IsResourceCarrier(parent))
                    {
                        rangedMotionSource = data.rangedMotionSource;
                        rangedMotionRefined = data.rangedMotionRefined;
                    }
                    else if (parent.ModProjectile != null && !data.EchoCompletedAI && data.rangedMotionSource)
                    {
                        // 首次 AI 中生成射弹后才设 heldProj 的模组载体，等父 AI 完成再确认。
                        rangedMotionPendingParent = parent;
                        rangedMotionParentIdentity = parent.identity;
                        rangedMotionParentType = parent.type;
                        rangedMotionRocketAtSpawn = Main.player[p.owner].TryGetModPlayer(out VortexQuiverPlayer state) &&
                            state.IsEquipped(AmmoID.Rocket);
                    }
                }
            }
            // 包括暂时不 friendly 的载体；其射弹可能在后续更新中生成。
            if (rangedMotionSource) p.netUpdate = true;
        }

        private bool IsIndependentRangedFlight(Projectile p) => p.active && p.friendly && !p.hostile &&
            !p.npcProj && !IsExtraHit && !IsTimeEchoAttack && !p.minion && !p.sentry && p.minionSlots == 0 &&
            !Main.projPet[p.type] && !ProjectileID.Sets.LightPet[p.type] && !ProjectileID.Sets.IsAWhip[p.type] &&
            !ProjectileID.Sets.MinionShot[p.type] && !ProjectileID.Sets.SentryShot[p.type] &&
            p.aiStyle != ProjAIStyleID.HeldProjectile && p.aiStyle != ProjAIStyleID.ThickLaser &&
            p.aiStyle != ProjAIStyleID.Hook && !TimeEchoAttackSystem.IsResourceCarrier(p) &&
            // 爆炸阶段不再移动；不能把整个 Explosive AI 禁用，普通火箭也使用它。
            !(p.timeLeft <= 1 && (p.aiStyle == ProjAIStyleID.Explosive || ProjectileID.Sets.Explosive[p.type])) &&
            ProjectileLoader.ShouldUpdatePosition(p);

        private void RangedMotion_RestoreVelocity(Projectile p)
        {
            if (rangedMotionApplied == 1) return;
            // 使用当前速度，保留上一更新的反弹/碰撞，不恢复过时的自然速度缓存。
            p.velocity /= rangedMotionApplied;
            rangedMotionApplied = 1;
        }

        private void RangedMotion_ApplyVelocity(Projectile p)
        {
            if (rangedMotionPendingParent is { } parent)
            {
                var data = parent.GetGlobalProjectile<GlobalProjectile>();
                if (data.EchoCompletedAI || !parent.active || TimeEchoAttackSystem.IsResourceCarrier(parent) ||
                    parent.identity != rangedMotionParentIdentity || parent.type != rangedMotionParentType || parent.owner != p.owner)
                {
                    rangedMotionPendingParent = null;
                    if (parent.identity == rangedMotionParentIdentity && parent.type == rangedMotionParentType &&
                        parent.owner == p.owner && !data.IsExtraHit && !data.IsTimeEchoAttack &&
                        TimeEchoAttackSystem.IsResourceCarrier(parent))
                    {
                        rangedMotionSource = data.rangedMotionSource;
                        rangedMotionRefined = data.rangedMotionRefined;
                        if (vortexAmmoCategory == AmmoID.Rocket) vortexAmmoEmpowered = rangedMotionRocketAtSpawn;
                        p.netUpdate = true;
                    }
                }
            }
            if (rangedMotionApplied != 1 || !rangedMotionSource || !IsIndependentRangedFlight(p)) return;
            byte factor = 1;
            if (rangedMotionRefined) factor *= (byte)RefinementRangedPrefix.ShootSpeedMult;
            if (vortexAmmoCategory == AmmoID.Rocket && vortexAmmoEmpowered) factor *= 2;
            if (factor == 1 || p.velocity == Vector2.Zero) return;
            p.velocity *= factor;
            rangedMotionApplied = factor;
        }

        private void RangedMotion_Write(BinaryWriter writer)
        {
            writer.Write((byte)((rangedMotionSource ? 1 : 0) | (rangedMotionRefined ? 2 : 0)));
            writer.Write(rangedMotionApplied);
        }

        private void RangedMotion_Read(BinaryReader reader)
        {
            byte flags = reader.ReadByte();
            rangedMotionSource = (flags & 1) != 0;
            rangedMotionRefined = rangedMotionSource && (flags & 2) != 0;
            byte applied = reader.ReadByte();
            rangedMotionApplied = applied is 2 or 4 ? applied : (byte)1;
        }
    }
}
