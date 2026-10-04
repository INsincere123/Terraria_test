using Terraria;
using Microsoft.Xna.Framework;
using System;
using Terraria.ID;
using TestMod.Common.Utilities;
using TestMod.Common.Mechanics.AccessoryEffects;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   星尘龙：保留原版生命周期，只替换攻击运动
        // ══════════════════════════════════════════════════════════════
        private void ApplyStardustDragonTracking(Projectile projectile)
        {
            if (!_dragonFlightPrepared) return;
            bool authority = projectile.owner == Main.myPlayer;
            // 原版返回传送/世界边界修正后清理阶段，本次保留原版返回运动。
            if (Vector2.DistanceSquared(projectile.Center, _dragonIncomingCenter) > 256f)
            {
                MarkTrackingActivity(projectile, false, _dragonFlight.Reset());
                _dragonTrackingCooldown = 0;
                return;
            }

            bool changed = false;
            if (authority)
            {
                int index = _trackingTargetCache.FindNearest(projectile, projectile.Center, 7200f,
                    out bool replaced, prioritizeMinionTarget: true, retargetFrames: 45);
                changed = index >= 0 ? _dragonFlight.SetTarget(index, Main.npc[index].type, replaced) : _dragonFlight.Reset();
            }
            NPC target = _dragonFlight.ResolveTarget(projectile, projectile.Center, 7200f);
            if (target == null)
            {
                changed |= _dragonFlight.Reset();
                _dragonTrackingCooldown = 0;
                MarkTrackingActivity(projectile, false, changed);
                return;
            }

            bool coast = _dragonTrackingCooldown > 0;
            if (coast) _dragonTrackingCooldown--;
            // localAI[0] 是原版龙链累计段数，原版段间距为 16*scale。
            float chainLength = Math.Max(1f, projectile.localAI[0]) * 16f * projectile.scale;
            projectile.velocity = _dragonFlight.Update(projectile.Center, projectile.Size, target.Hitbox,
                target.velocity, _dragonIncomingVelocity, chainLength, 1f / projectile.MaxUpdates,
                DragonFlightSettings.Stardust, authority, coast, out bool phaseChanged);
            projectile.rotation = projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Math.Abs(projectile.velocity.X) > 0.01f)
                projectile.direction = projectile.spriteDirection = projectile.velocity.X > 0f ? 1 : -1;
            MarkTrackingActivity(projectile, true, changed || phaseChanged);
        }

        private DragonFlightController _dragonFlight;
        private Vector2 _dragonIncomingVelocity, _dragonIncomingCenter;
        private bool _dragonFlightPrepared;

        private void PrepareStardustDragonFlight(Projectile projectile)
        {
            _dragonFlightPrepared = false;
            if (projectile.type != ProjectileID.StardustDragon1) return;
            bool enabled = projectile.owner >= 0 && projectile.owner < Main.maxPlayers
                && Main.player[projectile.owner].active && !Main.player[projectile.owner].dead;
            if (!enabled)
            {
                MarkTrackingActivity(projectile, false, _dragonFlight.Reset());
                _dragonTrackingCooldown = 0;
                return;
            }
            _dragonIncomingVelocity = projectile.velocity;
            _dragonIncomingCenter = projectile.Center;
            _dragonFlightPrepared = true;
        }

        // 沙漠虎仍沿用之前的追踪，不受龙头运动优化影响。
        private void ApplyLegacyTigerTracking(Projectile projectile)
        {
            // ── 如果还在冷却中，递减并直接跳出，靠惯性飞行 ──
            if (_dragonTrackingCooldown > 0)
            {
                _dragonTrackingCooldown--;
                return;
            }

            const float maxRange        = 7200f;
            const float minSpeed        = 42f;
            const float maxSpeed        = 150f;
            const float lerpAmount      = 0.1f;
            const float correctionForce = 0.5f;
            // 在完整索敌范围内保留目标 45 个游戏帧，空场每 6 帧重试。
            // 不再因目标超过 900px 就逐更新扫描；指定目标切换和目标失效立即响应。
            int targetIndex = FindTrackingTarget(projectile, projectile.Center, maxRange,
                prioritizeMinionTarget: true, retargetFrames: 45);
            if (targetIndex < 0) return;

            NPC target = Main.npc[targetIndex];
            Vector2 toTarget = target.Center - projectile.Center;
            float dist = toTarget.Length();
            if (dist < 1f) return;

            // 预测目标未来位置
            float currentSpeed  = Math.Max(projectile.velocity.Length(), 1f);
            float predictFrames = MathHelper.Clamp(dist / currentSpeed, 0f, 25f);
            Vector2 toPredicted = (target.Center + target.velocity * predictFrames * 0.65f) - projectile.Center;
            if (toPredicted.LengthSquared() < 1f) toPredicted = toTarget;
            toPredicted.Normalize();

            float desiredSpeed = MathHelper.Clamp(minSpeed + dist / 30f, minSpeed, maxSpeed);
            projectile.velocity  = Vector2.Lerp(projectile.velocity, toPredicted * desiredSpeed, lerpAmount);
            projectile.velocity += toPredicted * correctionForce;

            // 速度钳制
            float finalSpeed = projectile.velocity.Length();
            if (finalSpeed > maxSpeed + 8f)
                projectile.velocity = projectile.velocity / finalSpeed * (maxSpeed + 8f);

        }

        // ══════════════════════════════════════════════════════════════
        //   星尘龙命中效果：两轮链式溅射
        // ══════════════════════════════════════════════════════════════
        private void HandleStardustDragonHit(Player player, NPC target, int damageDone)
        {
            // 第一轮：500f 内最多 6 个敌人，80% 伤害
            int chainCount = 0;
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                if (Vector2.Distance(target.Center, npc.Center) > 500f) continue;

                ExtraHitEffect.StrikeRatio(player, npc, damageDone, 0.8f);
                SpawnSplitVisual(target.Center, npc.Center);
                if (++chainCount >= 6) break;
            }

            // 第二轮：300f 内所有敌人，50% 伤害
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                if (Vector2.Distance(target.Center, npc.Center) >= 300f) continue;

                ExtraHitEffect.StrikeRatio(player, npc, damageDone, 0.5f);
                SpawnSplitVisual(target.Center, npc.Center);
            }
        }
    }
}
