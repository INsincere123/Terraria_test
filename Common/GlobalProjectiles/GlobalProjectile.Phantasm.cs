using Terraria;
using Microsoft.Xna.Framework;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   通用高阶追踪
        //   用于:MinionShot 召唤物射弹及星云烈焰；排除名单和专用 AI 优先
        // ══════════════════════════════════════════════════════════════
        private void ApplyHighTierTracking(Projectile projectile, float minSpeed, float maxSpeed,
            float lerpAmount, float extraCorrection, bool prioritizeMinionTarget = false)
        {
            int targetIndex = FindTrackingTarget(projectile, projectile.Center, 2400f, prioritizeMinionTarget);
            if (targetIndex < 0) return;

            NPC target = Main.npc[targetIndex];
            Vector2 toTarget = target.Center - projectile.Center;
            if (toTarget.LengthSquared() <= 1f) return;

            float dist = toTarget.Length();
            toTarget /= dist;

            float desiredSpeed = MathHelper.Clamp(minSpeed + dist / 45f, minSpeed, maxSpeed);
            projectile.velocity  = Vector2.Lerp(projectile.velocity, toTarget * desiredSpeed, lerpAmount);
            projectile.velocity += toTarget * extraCorrection;
        }

        // ══════════════════════════════════════════════════════════════
        //   [已废弃] ApplyGenericMinionTracking
        //
        //   原本用于所有普通召唤物的无差别追踪,但存在弊端:
        //   对"保持距离发射射弹"的召唤物(如星尘细胞)会导致贴敌不开枪。
        //
        //   已由新的三分类系统替代,见 GlobalProjectile.MinionClassify.cs:
        //     · 冲撞型  → ApplyContactMinionTracking
        //     · 射击型本体 → 保持 vanilla
        //     · MinionShot → ApplyHighTierTracking
        // ══════════════════════════════════════════════════════════════
    }
}
