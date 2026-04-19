using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace 武器test
{
    public partial class MyGlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   幻影弓命中效果：链式跳跃 + 范围爆炸 + 粒子特效
        // ══════════════════════════════════════════════════════════════
        private void HandlePhantasmArrowHit(NPC target, int damageDone)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // 链式跳跃：400f 内最近 3 个其他敌人，60% 伤害
                int chainCount = 0;
                for (int i = 0; i < Main.npc.Length; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, npc.Center) > 400f) continue;

                    npc.SimpleStrikeNPC((int)(damageDone * 0.6f), 0, false, 0, null, false, 0);
                    SpawnSplitVisual(target.Center, npc.Center);
                    if (++chainCount >= 3) break;
                }

                // 范围爆炸：300f 内其他敌人，40% 伤害
                for (int i = 0; i < Main.npc.Length; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, npc.Center) > 300f) continue;

                    npc.SimpleStrikeNPC((int)(damageDone * 0.4f), 0, false, 0, null, false, 0);
                }
            }

            // 爆炸粒子特效
            if (Main.netMode != NetmodeID.Server)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = MathHelper.TwoPi / 12f * i;
                    Vector2 vel = new Vector2(
                        (float)System.Math.Cos(angle) * Main.rand.NextFloat(3f, 7f),
                        (float)System.Math.Sin(angle) * Main.rand.NextFloat(3f, 7f));
                    Dust.NewDustPerfect(target.Center, DustID.BlueFairy, vel, 0,
                        Color.Cyan, Main.rand.NextFloat(1f, 1.8f));
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   通用高阶追踪（泰拉棱镜 / 乌鸦 / 沙漠虎 / 星尘细胞子细胞）
        // ══════════════════════════════════════════════════════════════
        private void ApplyHighTierTracking(Projectile projectile, float minSpeed, float maxSpeed,
            float lerpAmount, float extraCorrection)
        {
            int targetIndex = AcquireNearestTarget(projectile.Center, 2400f);
            if (targetIndex < 0) return;

            NPC target = Main.npc[targetIndex];
            Vector2 toTarget = target.Center - projectile.Center;
            if (toTarget.LengthSquared() <= 1f) return;

            float dist = toTarget.Length();
            toTarget.Normalize();

            float desiredSpeed = MathHelper.Clamp(minSpeed + dist / 45f, minSpeed, maxSpeed);
            projectile.velocity  = Vector2.Lerp(projectile.velocity, toTarget * desiredSpeed, lerpAmount);
            projectile.velocity += toTarget * extraCorrection;
            projectile.netUpdate = true;
        }

        // ══════════════════════════════════════════════════════════════
        //   普通召唤物追踪：以玩家为圆心寻敌
        // ══════════════════════════════════════════════════════════════
        private void ApplyGenericMinionTracking(Projectile projectile, Player player, float maxRange)
        {
            int targetIndex = AcquireNearestTarget(player.Center, maxRange);

            if (targetIndex >= 0)
            {
                NPC target = Main.npc[targetIndex];
                Vector2 toTarget = target.Center - projectile.Center;
                if (toTarget.LengthSquared() <= 1f) return;

                float dist = toTarget.Length();
                toTarget.Normalize();

                float desiredSpeed = MathHelper.Clamp(11f + dist / 65f, 13f, 26f);
                projectile.velocity = Vector2.Lerp(projectile.velocity, toTarget * desiredSpeed, 0.09f);
            }
            // 无目标时保持飞行惯性
            else if (projectile.velocity.LengthSquared() > 0.01f)
            {
                Vector2 dir = Vector2.Normalize(projectile.velocity);
                projectile.velocity = Vector2.Lerp(projectile.velocity, dir * 14f, 0.06f);
            }
        }
    }
}
