using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace 武器test.Projectiles
{
    /// <summary>
    /// 幻影弓强化专用弹射物
    /// 特性：穿墙 / 穿5个 / 平滑追踪 / 破甲debuff / 独立无敌帧防骗伤
    /// 触发原版幻影箭：每帧设置 player.phantasmTime = 2
    /// </summary>
    public class PhantasmSpecialArrowProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_935"; // 借用夜明箭贴图

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type]     = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width       = 10;
            Projectile.height      = 10;
            Projectile.friendly    = true;
            Projectile.DamageType  = DamageClass.Ranged;
            Projectile.arrow       = true;
            Projectile.tileCollide = false;     // 穿墙
            Projectile.penetrate   = 5;         // 穿透5个敌人后消失
            Projectile.timeLeft    = 300;       // 5秒存活
            Projectile.light       = 0.5f;
            Projectile.extraUpdates = 1;

            // 独立无敌帧，多支箭同时命中同一敌人各自结算，不互相骗伤
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown  = 5;
        }

        // ══════════════════════════════════════════════════════════════
        //   AI — 每帧触发幻影箭 + 平滑追踪 + 粒子尾迹
        // ══════════════════════════════════════════════════════════════
        public override void AI()
        {
            // 触发原版幻影箭生成逻辑（参考灾厄 RiftburstBow）
            Main.player[Projectile.owner].phantasmTime = 2;

            // 贴图朝向修正（夜明箭贴图竖直，需要 +PiOver2）
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // ai[0] 是命中后的不追踪冷却计时器
            // 命中时设为10帧，让箭矢穿过去后再寻找下一目标
            if (Projectile.ai[0] > 0)
            {
                Projectile.ai[0]--;
            }
            else if (Projectile.timeLeft < 295) // 生成后5帧才开始追踪，保留散射方向
            {
                int targetIndex = FindNearestTargetNotOnCooldown(1800f);
                if (targetIndex >= 0)
                {
                    NPC target = Main.npc[targetIndex];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float dist = toTarget.Length();

                    if (dist > 1f)
                    {
                        toTarget.Normalize();

                        const float desiredSpeed = 22f;
                        const float lerpAmount   = 0.08f; // 平滑转向，不急转

                        Projectile.velocity = Vector2.Lerp(
                            Projectile.velocity,
                            toTarget * desiredSpeed,
                            lerpAmount);

                        // 速度保底，防止 Lerp 后速度被拉低
                        float speed = Projectile.velocity.Length();
                        if (speed < desiredSpeed * 0.8f)
                            Projectile.velocity = Projectile.velocity / speed * (desiredSpeed * 0.8f);
                    }
                }
            }

            // 粒子尾迹
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.BlueFairy,
                    Projectile.velocity.X * -0.2f,
                    Projectile.velocity.Y * -0.2f,
                    0, Color.MediumPurple, 0.8f);
                dust.noGravity = true;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   OnHitNPC — 破甲debuff叠加 + 触发冷却
        //   链式跳跃和范围爆炸在 MyGlobalProjectile.OnHitNPC 里统一处理
        // ══════════════════════════════════════════════════════════════
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 命中后10帧不追踪，让箭矢自然穿过去
            Projectile.ai[0] = 10f;

            // 破甲debuff叠加（最多10层）
            if (MyGlobalNPC.GetArmorShredStacks(target.whoAmI) < 10)
                MyGlobalNPC.AddArmorShredStack(target.whoAmI);

            target.AddBuff(ModContent.BuffType<Buffs.ArmorShredDebuff>(), 180);
        }

        // ══════════════════════════════════════════════════════════════
        //   OnKill — 消失爆炸粒子（仅客户端）
        // ══════════════════════════════════════════════════════════════
        public override void OnKill(int timeLeft)
        {
            if (Main.netMode == NetmodeID.Server) return;

            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi / 8f * i;
                Vector2 vel = new Vector2(
                    (float)Math.Cos(angle) * Main.rand.NextFloat(2f, 5f),
                    (float)Math.Sin(angle) * Main.rand.NextFloat(2f, 5f));
                Dust.NewDustPerfect(Projectile.Center, DustID.BlueFairy, vel, 0, Color.Cyan, 1.2f);
            }
        }

        /// <summary>
        /// 找最近的可追踪 NPC，跳过命中冷却中的敌人
        /// 这样穿透后会自动转向下一个目标而不是粘着刚命中的同一个
        /// </summary>
        private int FindNearestTargetNotOnCooldown(float maxRange)
        {
            int bestIndex    = -1;
            float bestDistSq = maxRange * maxRange;

            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                if (Projectile.localNPCImmunity[i] > 0) continue; // 跳过刚命中的

                float distSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex  = i;
                }
            }
            return bestIndex;
        }
    }
}