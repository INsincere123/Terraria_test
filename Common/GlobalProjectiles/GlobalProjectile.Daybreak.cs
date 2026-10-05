using Terraria;
using Microsoft.Xna.Framework;
using System;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   破晓之光矛追踪：带重力抵消
        // ══════════════════════════════════════════════════════════════
        private void ApplyDaybreakTracking(Projectile projectile)
        {
            // 发射后前 35 次 AI 更新保持原方向；沿用既有起飞时长。
            if (_daybreakTrackDelay < 35)
            {
                _daybreakTrackDelay++;
                return;
            }

            const float trackRange      = 1000f;
            const float minSpeed        = 41.4f;
            const float maxSpeed        = 69f;
            const float lerpAmount      = 0.125f;
            const float correctionForce = 0.40f;
            const float finalSpeedLimit = 74.75f;

            int targetIndex = FindTrackingTarget(projectile, projectile.Center, trackRange);
            if (targetIndex < 0) return;

            NPC target = Main.npc[targetIndex];
            Vector2 toTarget = target.Center - projectile.Center;
            float dist = toTarget.Length();
            if (dist < 1f) return;

            // 预测目标未来位置
            float currentSpeed  = Math.Max(projectile.velocity.Length(), 1f);
            float predictFrames = MathHelper.Clamp(dist / currentSpeed, 0f, 15f);
            Vector2 toPredicted = (target.Center + target.velocity * predictFrames * 0.35f) - projectile.Center;
            if (toPredicted.LengthSquared() < 1f) toPredicted = toTarget;
            toPredicted.Normalize();

            float desiredSpeed = MathHelper.Clamp(minSpeed + dist / 40f, minSpeed, maxSpeed);
            projectile.velocity  = Vector2.Lerp(projectile.velocity, toPredicted * desiredSpeed, lerpAmount);
            projectile.velocity += toPredicted * correctionForce;

            float finalSpeed = projectile.velocity.Length();
            if (finalSpeed > finalSpeedLimit)
                projectile.velocity = projectile.velocity / finalSpeed * finalSpeedLimit;

            projectile.velocity.Y -= 0.3f; // 抵消重力
        }
    }
}
