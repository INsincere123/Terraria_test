using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System;

namespace 武器test
{
    public partial class MyGlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   破晓之光矛追踪：带重力抵消
        // ══════════════════════════════════════════════════════════════
        private void ApplyDaybreakTracking(Projectile projectile)
        {
            // 发射后前45帧保持原方向，不追踪
            if (_daybreakTrackDelay < 45)
            {
                _daybreakTrackDelay++;
                return;
            }

            const float trackRange      = 800f;
            const float minSpeed        = 36f;
            const float maxSpeed        = 60f;
            const float lerpAmount      = 0.1f;
            const float correctionForce = 0.32f;

            int targetIndex = AcquireNearestTarget(projectile.Center, trackRange);
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
            if (finalSpeed > maxSpeed + 5f)
                projectile.velocity = projectile.velocity / finalSpeed * (maxSpeed + 5f);

            projectile.velocity.Y -= 0.3f; // 抵消重力
            projectile.netUpdate = true;
        }

        // ══════════════════════════════════════════════════════════════
        //   破晓之光太阳爆发：范围溅射 + 三层粒子特效
        // ══════════════════════════════════════════════════════════════
        private void HandleDaybreakBurst(NPC target, int damageDone)
        {
            // 范围溅射：300f 内其他敌人，50% 伤害
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < Main.npc.Length; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, npc.Center) > 300f) continue;

                    npc.SimpleStrikeNPC((int)(damageDone * 0.5f), 0);
                }
            }

            if (Main.netMode == NetmodeID.Server) return;

            // 第一层：密集橙红火焰环
            for (int i = 0; i < 24; i++)
            {
                float angle = MathHelper.TwoPi / 24f * i;
                Vector2 vel = new Vector2(
                    (float)Math.Cos(angle) * Main.rand.NextFloat(5f, 10f),
                    (float)Math.Sin(angle) * Main.rand.NextFloat(5f, 10f));
                Dust.NewDustPerfect(target.Center, DustID.SolarFlare, vel, 0,
                    Color.OrangeRed, Main.rand.NextFloat(1.2f, 2f));
            }

            // 第二层：慢速金色光点
            for (int i = 0; i < 16; i++)
            {
                float angle = MathHelper.TwoPi / 16f * i;
                Vector2 vel = new Vector2(
                    (float)Math.Cos(angle) * Main.rand.NextFloat(2f, 5f),
                    (float)Math.Sin(angle) * Main.rand.NextFloat(2f, 5f));
                Dust.NewDustPerfect(target.Center, DustID.FireworkFountain_Yellow, vel, 0,
                    Color.Gold, Main.rand.NextFloat(1f, 1.6f));
            }

            // 中心爆炸白闪
            for (int i = 0; i < 8; i++)
            {
                Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.SolarFlare, Vector2.Zero, 0, Color.White, 2f);
            }
        }
    }
}
