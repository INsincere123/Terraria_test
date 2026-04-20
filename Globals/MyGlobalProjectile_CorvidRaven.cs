using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System;

namespace 武器test
{
    public partial class MyGlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   乌鸦法杖召唤物 AI（忠实移植自灾厄 PowerfulRaven）
        //   对应 projectile ID 317 (ProjectileID.Raven)
        //
        //   localAI[0]: 帧计时器（累加，% 45 判断攻击周期）
        //   localAI[1]: 初始化标记
        //   —— 使用 localAI 而非 ai[]，避免与 vanilla 乌鸦自身的状态机冲突
        //
        //   攻击周期（45帧）：
        //     Phase  0~27 : 减速阶段 —— velocity *= 0.95f，消化上一次冲刺
        //     Phase 28~44 : 冲刺阶段 —— phase==28 赋速(≥34f)，之后全程保速撞敌
        // ══════════════════════════════════════════════════════════════

        private const float CORVID_DIST_CHECK    = 3200f;
        private const float CORVID_TELEPORT_DIST = 2700f;
        private const float CORVID_SEPARATION    = 2000f;
        private const float CORVID_DASH_MIN_SPD  = 34f;
        private const int   CORVID_CYCLE         = 45;
        private const int   CORVID_DASH_START    = 28;

        private void ApplyCorvidRavenAI(Projectile projectile, Player player)
        {
            // ── 首帧初始化 ───────────────────────────────────────────
            if (projectile.localAI[1] == 0f)
                projectile.localAI[1] = 1f;

            // ── 防止超时消失 ─────────────────────────────────────────
            projectile.timeLeft = Math.Max(projectile.timeLeft, 10);

            // ── 防聚堆（模拟 MinionAntiClump）────────────────────────
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (i == projectile.whoAmI || !other.active) continue;
                if (other.type != projectile.type || other.owner != projectile.owner) continue;
                float sep = Vector2.Distance(projectile.Center, other.Center);
                if (sep < 40f && sep > 0f)
                    projectile.velocity += (projectile.Center - other.Center) / sep * 0.5f;
            }

            // ── 寻找最近目标 ─────────────────────────────────────────
            int targetIdx = AcquireNearestTarget(player.Center, CORVID_DIST_CHECK);
            NPC  target   = targetIdx >= 0 ? Main.npc[targetIdx] : null;

            if (target != null)
            {
                projectile.localAI[0] += 1f;

                // 超出传送距离 → 直接传送到目标旁
                if (projectile.Distance(target.Center) > CORVID_TELEPORT_DIST)
                {
                    projectile.Center = target.Center +
                        Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2()
                        * target.Size * 1.3f;
                    projectile.netUpdate = true;
                }
                else
                {
                    float phase = projectile.localAI[0] % CORVID_CYCLE;

                    if (phase >= CORVID_DASH_START)
                    {
                        // ── 冲刺阶段（Phase 28~44）──────────────────
                        // 仅在 phase 进入 28 的瞬间赋予冲刺速度
                        // 之后 17 帧不减速，维持高速直接撞敌
                        if (phase == CORVID_DASH_START || projectile.Distance(target.Center) > 450f)
                        {
                            if (Main.rand.NextBool(6))
                            {
                                // 1/6 概率传送 + 粒子爆散
                                projectile.Center = target.Center +
                                    Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2()
                                    * target.Size * 1.3f;
                                projectile.netUpdate = true;

                                if (Main.netMode != NetmodeID.Server)
                                {
                                    for (int i = 0; i < 40; i++)
                                    {
                                        float angle = MathHelper.TwoPi / 40f * i;
                                        float lerp  = MathHelper.Lerp(0f, 1f,
                                            (float)Math.Sin(i / 8f * MathHelper.TwoPi) * 0.5f + 0.5f);
                                        Dust d = Dust.NewDustPerfect(projectile.position, DustID.Torch);
                                        d.velocity  = Vector2.Lerp(Vector2.Zero, angle.ToRotationVector2() * 6f, lerp);
                                        d.noGravity = true;
                                    }
                                }
                            }

                            // 朝目标的冲刺速度，最低 34f
                            projectile.velocity = (target.Center - projectile.Center) / 50f;
                            if (projectile.velocity.Length() < CORVID_DASH_MIN_SPD)
                                projectile.velocity = projectile.velocity.SafeNormalize(Vector2.UnitX)
                                    * CORVID_DASH_MIN_SPD;

                            // 翅膀粒子
                            if (Main.netMode != NetmodeID.Server)
                            {
                                for (int i = 0; i < 20; i++)
                                {
                                    float angle = MathHelper.TwoPi / 20f * i;
                                    Dust d = Dust.NewDustPerfect(
                                        projectile.position + angle.ToRotationVector2()
                                            .RotatedBy(projectile.rotation) * new Vector2(14f, 21f),
                                        DustID.Torch);
                                    d.velocity  = angle.ToRotationVector2().RotatedBy(projectile.rotation) * 2f;
                                    d.noGravity = true;
                                }
                            }
                        }

                        // 冲刺帧定格 + 白光
                        projectile.frame = Main.projFrames[projectile.type] - 1;
                        Lighting.AddLight(projectile.Center, 1f, 1f, 1f);
                    }
                    else
                    {
                        // ── 减速阶段（Phase 0~27）────────────────────
                        projectile.velocity *= 0.95f;
                        projectile.rotation  = projectile.rotation.AngleTowards(0f, 0.3f);

                        projectile.frameCounter++;
                        if (projectile.frameCounter > 6)
                        {
                            projectile.frame++;
                            projectile.frameCounter = 0;
                        }
                        if (projectile.frame >= Main.projFrames[projectile.type] - 1)
                            projectile.frame = 0;
                    }
                }
            }
            else
            {
                // ── 无目标：旋转归零 + 返回玩家 ─────────────────────
                projectile.rotation = projectile.rotation.AngleTowards(0f, 0.2f);

                projectile.frameCounter++;
                if (projectile.frameCounter > 6)
                {
                    projectile.frame++;
                    projectile.frameCounter = 0;
                }
                if (projectile.frame >= Main.projFrames[projectile.type] - 1)
                    projectile.frame = 0;

                if (projectile.Distance(player.Center) > CORVID_SEPARATION)
                {
                    projectile.Center    = player.Center;
                    projectile.velocity  = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * 12f;
                    projectile.netUpdate = true;
                }
                else if (!projectile.WithinRange(player.Center, 90f))
                {
                    Vector2 toPlayer = player.Center - projectile.Center;
                    if (toPlayer.LengthSquared() > 0f) toPlayer.Normalize();
                    projectile.velocity = (projectile.velocity * 19f + toPlayer * 12f) / 20f;
                }
            }

            // ── 朝向 ─────────────────────────────────────────────────
            projectile.direction = projectile.spriteDirection =
                (projectile.velocity.X > 0).ToDirectionInt();
        }
    }
}
