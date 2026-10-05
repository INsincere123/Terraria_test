using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Utilities;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        private ProjectileTargetCache _trackingTargetCache;
        private ProjectileTargetCache _forcedHomingTargetCache;
        private bool _trackingTargetChanged;
        private bool _trackingHasTarget;
        private bool _trackingHasSyncSample;
        private ulong _trackingLastSyncFrame;
        private Vector2 _trackingLastSyncVelocity;
        private const ulong TrackingSyncFrames = 15;
        private const ulong TrackingMinTurnSyncFrames = 3;
        private const float TrackingTurnSyncCosine = 0.98480775f; // 累计转向 10 度。

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(IsHomingTagged);
            bitWriter.WriteBit(IsExtraHit);
            bitWriter.WriteBit(ExtraHitUseCrit);
            bitWriter.WriteBit(ExtraHitIgnoreDefense);
            bitWriter.WriteBit(subhandDynamicText);
            bitWriter.WriteBit(_bloodFeedPenetrationOverridden);
            BloodFeed_WritePenetration(binaryWriter);
            if (IsExtraHit) binaryWriter.Write(projectile.DamageType.Type);
            if (projectile.type == ProjectileID.EmpressBlade) WriteTerraprismaAI(binaryWriter);
            if (projectile.type == ProjectileID.AbigailMinion) WriteAbigailAI(projectile, binaryWriter);
            // 乌鸦用 localAI 保存冲刺周期，随 owner 的位置/速度同步供远端预测。
            if (projectile.type == ProjectileID.Raven) binaryWriter.Write(projectile.localAI[0]);
            if (projectile.type == ProjectileID.StardustDragon1)
            {
                _dragonFlight.Write(binaryWriter);
                binaryWriter.Write((byte)Math.Clamp(_dragonTrackingCooldown, 0, 3));
            }
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            IsHomingTagged = bitReader.ReadBit();
            IsExtraHit = bitReader.ReadBit();
            ExtraHitUseCrit = bitReader.ReadBit();
            ExtraHitIgnoreDefense = bitReader.ReadBit();
            subhandDynamicText = bitReader.ReadBit();
            BloodFeed_ReadPenetration(projectile, bitReader.ReadBit(), binaryReader);
            if (IsExtraHit)
            {
                projectile.DamageType = DamageClassLoader.GetDamageClass(binaryReader.ReadInt32()) ?? projectile.DamageType;
                projectile.CritChance = 0;
            }
            if (projectile.type == ProjectileID.EmpressBlade) ReadTerraprismaAI(projectile, binaryReader);
            if (projectile.type == ProjectileID.AbigailMinion) ReadAbigailAI(projectile, binaryReader);
            if (projectile.type == ProjectileID.Raven) projectile.localAI[0] = binaryReader.ReadSingle();
            if (projectile.type == ProjectileID.StardustDragon1)
            {
                _dragonFlight.Read(binaryReader);
                int cooldown = binaryReader.ReadByte();
                _dragonTrackingCooldown = _dragonFlight.Enabled && cooldown <= 3 ? cooldown : 0;
            }
        }

        // ModProjectile.AI、PreAI 接管和 PostAI 分支共用主缓存；强制追踪有独立缓存。
        internal static int FindTrackingTarget(Projectile projectile, Vector2 origin, float range,
            bool prioritizeMinionTarget = false, ulong retargetFrames = 6,
            int preferredTarget = -1, float preferredRange = 0f, bool includePreferredBoundary = false)
        {
            var state = projectile.GetGlobalProjectile<GlobalProjectile>();
            int target = state._trackingTargetCache.FindNearest(projectile, origin, range, out bool changed,
                prioritizeMinionTarget, retargetFrames, preferredTarget, preferredRange,
                includePreferredBoundary: includePreferredBoundary);
            state._trackingTargetChanged |= changed;
            state._trackingHasTarget |= target >= 0;
            return target;
        }

        private void ResetTrackingUpdate()
        {
            _trackingTargetChanged = false;
            _trackingHasTarget = false;
        }

        internal static void MarkTrackingActivity(Projectile projectile, bool hasTarget, bool changed)
        {
            var state = projectile.GetGlobalProjectile<GlobalProjectile>();
            state._trackingHasTarget |= hasTarget;
            state._trackingTargetChanged |= changed;
        }

        private void RequestTrackingSync(Projectile projectile)
        {
            // 最终转向/时缓/强制追踪结束后采样；不清除其他 AI 或钩子的 netUpdate。
            if (projectile.owner != Main.myPlayer || (!_trackingHasTarget && !_trackingTargetChanged)) return;

            ulong elapsed = Main.GameUpdateCount - _trackingLastSyncFrame;
            bool needsSync = !_trackingHasSyncSample || _trackingTargetChanged || elapsed >= TrackingSyncFrames;
            if (!needsSync && elapsed >= TrackingMinTurnSyncFrames)
            {
                float speed = projectile.velocity.Length();
                float oldSpeed = _trackingLastSyncVelocity.Length();
                needsSync = Math.Abs(speed - oldSpeed) >= Math.Max(2f, oldSpeed * 0.1f)
                    || (speed > 0.01f && oldSpeed > 0.01f
                        && Vector2.Dot(projectile.velocity, _trackingLastSyncVelocity) / (speed * oldSpeed)
                            <= TrackingTurnSyncCosine);
            }

            if (projectile.netUpdate || needsSync)
            {
                projectile.netUpdate = true;
                _trackingHasSyncSample = true;
                _trackingLastSyncFrame = Main.GameUpdateCount;
                _trackingLastSyncVelocity = projectile.velocity;
            }
        }
    }
}
