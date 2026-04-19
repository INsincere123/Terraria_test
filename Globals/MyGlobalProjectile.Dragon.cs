using Terraria;
using Microsoft.Xna.Framework;
using System;

namespace 武器test
{
    public partial class MyGlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   星尘龙专属追踪：激进索敌 + 目标锁定 + 速度预测
        // ══════════════════════════════════════════════════════════════
        private void ApplyStardustDragonTracking(Projectile projectile)
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
            const float breakDistance   = 900f;

            _lockedTargetTimer--;

            // 锁定失效条件：无效目标 / 锁定超时 / 距离过远
            bool needReacquire =
                _lockedTargetIndex < 0 ||
                _lockedTargetIndex >= Main.npc.Length ||
                !Main.npc[_lockedTargetIndex].active ||
                !Main.npc[_lockedTargetIndex].CanBeChasedBy() ||
                _lockedTargetTimer <= 0 ||
                Vector2.Distance(projectile.Center, Main.npc[_lockedTargetIndex].Center) > breakDistance;

            if (needReacquire)
            {
                _lockedTargetIndex = AcquireNearestTarget(projectile.Center, maxRange);
                _lockedTargetTimer = LOCK_DURATION;
            }

            if (_lockedTargetIndex < 0) return;

            NPC target = Main.npc[_lockedTargetIndex];
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

            projectile.netUpdate = true;
        }

        // ══════════════════════════════════════════════════════════════
        //   星尘龙命中效果：两轮链式溅射
        // ══════════════════════════════════════════════════════════════
        private void HandleStardustDragonHit(NPC target, int damageDone)
        {
            // 第一轮：500f 内最多 6 个敌人，80% 伤害
            int chainCount = 0;
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                if (Vector2.Distance(target.Center, npc.Center) > 500f) continue;

                npc.SimpleStrikeNPC((int)(damageDone * 0.8f), 0);
                SpawnSplitVisual(target.Center, npc.Center);
                if (++chainCount >= 6) break;
            }

            // 第二轮：300f 内所有敌人，50% 伤害
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                if (Vector2.Distance(target.Center, npc.Center) >= 300f) continue;

                npc.SimpleStrikeNPC((int)(damageDone * 0.5f), 0);
                SpawnSplitVisual(target.Center, npc.Center);
            }
        }
    }
}
