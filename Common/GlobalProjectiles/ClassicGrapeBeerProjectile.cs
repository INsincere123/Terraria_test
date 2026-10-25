using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Systems;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalProjectiles
{
    /// <summary>独立冻结的经典葡萄啤酒：来源链与实际追踪分开维护，不借用灾厄运行状态。</summary>
    public class ClassicGrapeBeerProjectile : Terraria.ModLoader.GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        internal bool HasClassicSource;
        private bool _canInitialize;
        private bool _initialized;
        private bool _extraApplied;
        private int _lifetimeCap;
        private int _target = -1;
        private ulong _lastSync;
        private Vector2 _lastSyncVelocity;

        public override void SetDefaults(Projectile projectile)
        {
            HasClassicSource = _canInitialize = _initialized = _extraApplied = false;
            _target = -1;
            _lastSync = 0;
            _lifetimeCap = 0;
            _lastSyncVelocity = Vector2.Zero;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (source is EntitySource_ItemUse_WithAmmo use && use.Entity is Player player &&
                player.active && player.whoAmI == projectile.owner &&
                ClassicGrapeBeerPlayer.HasTrackingEffect(player) &&
                IsSupportedAmmo(use.Item.useAmmo))
                HasClassicSource = true;
            else if (source is EntitySource_Parent { Entity: Projectile parent } &&
                parent.owner == projectile.owner && parent.TryGetGlobalProjectile(out ClassicGrapeBeerProjectile state) &&
                (!parent.TryGetGlobalProjectile(out GlobalProjectile parentState) ||
                 !parentState.IsExtraHit && !parentState.IsTimeEchoAttack))
                HasClassicSource = state.HasClassicSource;

            // 载体只传标记；之后解除手持或改变伤害，也不追认其自身为飞行射弹。
            _canInitialize = HasClassicSource && projectile.owner >= 0 && projectile.owner < Main.maxPlayers &&
                projectile.damage > 0 && Main.player[projectile.owner].heldProj != projectile.whoAmI &&
                projectile.aiStyle != ProjAIStyleID.HeldProjectile && !ClassicGrapeBeerSystem.IsExcluded(projectile.type);
            if (HasClassicSource && projectile.owner == Main.myPlayer) projectile.netUpdate = true;
        }

        internal static bool IsSupportedAmmo(int ammo)
            => ammo == AmmoID.Bullet || ammo == AmmoID.Arrow || ammo == AmmoID.Dart || ammo == AmmoID.Rocket;

        private bool IsEligible(Projectile p)
            => HasClassicSource && _canInitialize && p.owner >= 0 && p.owner < Main.maxPlayers &&
                Main.player[p.owner].active &&
                !p.npcProj && !p.trap && p.damage > 0 &&
                Main.player[p.owner].heldProj != p.whoAmI && p.aiStyle != ProjAIStyleID.HeldProjectile &&
                !ClassicGrapeBeerSystem.IsExcluded(p.type) &&
                (!p.TryGetGlobalProjectile(out GlobalProjectile state) || !state.IsExtraHit && !state.IsTimeEchoAttack);

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.TryGetGlobalProjectile(out GlobalProjectile state) &&
                (state.IsExtraHit || state.IsTimeEchoAttack))
            {
                HasClassicSource = false;
                SetExtraUpdate(projectile, false);
                return true;
            }
            // OnSpawn 期间其他 Global 的来源标记可能尚未完成；首次 AI 再确定是否可接管。
            if (!_initialized && IsEligible(projectile)) Initialize(projectile);
            return true;
        }

        private void Initialize(Projectile p)
        {
            _initialized = true;
            if (_lifetimeCap == 0) _lifetimeCap = 300 * p.MaxUpdates;
            p.timeLeft = Math.Min(p.timeLeft, _lifetimeCap);
            p.usesLocalNPCImmunity = true;
            p.localNPCHitCooldown = -1;
        }

        public override void AI(Projectile p)
        {
            if (!_initialized || !IsEligible(p) || !p.friendly)
            {
                SetExtraUpdate(p, false);
                return;
            }

            int target = FindTarget(p);
            SetExtraUpdate(p, target >= 0);
            if (target >= 0)
                p.velocity = Steer(p.velocity, Main.npc[target].Center - p.Center);

            // 目标改变立即同步；稳定追踪每 15 游戏 tick 或显著转弯后同步，不随 extraUpdates 翻倍。
            ulong elapsed = Main.GameUpdateCount - _lastSync;
            bool changed = target != _target;
            _target = target;
            if (p.owner == Main.myPlayer && (changed || target >= 0 &&
                (elapsed >= 15 || elapsed >= 3 && Vector2.DistanceSquared(p.velocity, _lastSyncVelocity) >= 4f)))
            {
                p.netUpdate = true;
                _lastSync = Main.GameUpdateCount;
                _lastSyncVelocity = p.velocity;
            }
        }

        internal static Vector2 Steer(Vector2 velocity, Vector2 toTarget)
            => (velocity * 20f + toTarget.SafeNormalize(Vector2.UnitY) * 12f) / 21f;

        internal static int FindTarget(Projectile p)
        {
            int target = -1;
            float best = 25000f;
            foreach (NPC npc in Main.ActiveNPCs)
            {
                // 保留经典版的整数半宽/半高与静态免疫的 1600px 优先级惩罚。
                float range = 600f + npc.width / 2 + npc.height / 2;
                if (!npc.CanBeChasedBy(p) || !p.WithinRange(npc.Center, range) ||
                    p.localNPCImmunity[npc.whoAmI] != 0 || npc.immune[p.owner] > 0) continue;
                float distance = Vector2.Distance(p.Center, npc.Center);
                if (Projectile.perIDStaticNPCImmunity[p.type][npc.whoAmI] > Main.GameUpdateCount)
                    distance += 1600f;
                if (distance >= best || p.tileCollide && !Collision.CanHit(p.Center, 1, 1, npc.Center, 1, 1)) continue;
                best = distance;
                target = npc.whoAmI;
            }
            return target;
        }

        private void SetExtraUpdate(Projectile p, bool enabled)
        {
            if (_extraApplied == enabled) return;
            p.extraUpdates = Math.Max(0, p.extraUpdates + (enabled ? 1 : -1));
            _extraApplied = enabled;
        }

        public override bool? CanHitNPC(Projectile p, NPC target)
        {
            if (_initialized && p.usesLocalNPCImmunity && p.usesIDStaticNPCImmunity &&
                (p.localNPCImmunity[target.whoAmI] != 0 ||
                 Projectile.perIDStaticNPCImmunity[p.type][target.whoAmI] > Main.GameUpdateCount)) return false;
            return null;
        }

        public override void OnHitNPC(Projectile p, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 原版 Terraria 优先处理静态免疫；局部补齐经典版混合免疫，不影响其他弹幕。
            if (_initialized && p.usesLocalNPCImmunity && p.usesIDStaticNPCImmunity &&
                (p.penetrate != 1 || p.appliesImmunityTimeOnSingleHits))
            {
                p.localNPCImmunity[target.whoAmI] = p.localNPCHitCooldown;
                Projectile.perIDStaticNPCImmunity[p.type][target.whoAmI] =
                    Main.GameUpdateCount + (uint)p.idStaticNPCHitCooldown;
            }
        }

        public override void SendExtraAI(Projectile p, BitWriter bitWriter, BinaryWriter writer)
        {
            bitWriter.WriteBit(HasClassicSource);
            bitWriter.WriteBit(_canInitialize);
            bitWriter.WriteBit(_initialized);
            bitWriter.WriteBit(_extraApplied);
            if (_initialized) writer.Write(_lifetimeCap);
        }

        public override void ReceiveExtraAI(Projectile p, BitReader bitReader, BinaryReader reader)
        {
            HasClassicSource = bitReader.ReadBit();
            _canInitialize = bitReader.ReadBit();
            bool initialized = bitReader.ReadBit();
            bool extra = bitReader.ReadBit();
            if (initialized) _lifetimeCap = Math.Max(1, reader.ReadInt32());
            if (initialized && IsEligible(p))
            {
                if (!_initialized) Initialize(p);
                else p.timeLeft = Math.Min(p.timeLeft, _lifetimeCap);
            }
            SetExtraUpdate(p, extra && _initialized && IsEligible(p));
        }
    }
}
