using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace 武器test.Projectiles.Minions
{
    public class AntaresBeam : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;                           // 宽度24像素
            Projectile.height = 24;                          // 高度24像素
            Projectile.friendly = true;                      // 对玩家友好（不伤害玩家）
            Projectile.timeLeft = 180;                       // 存活时间180帧（≈3秒）
            Projectile.DamageType = DamageClass.Summon;      // 伤害类型：召唤伤害
            Projectile.MaxUpdates = 2;                       // 每帧更新2次（加快移动和AI）
            Projectile.tileCollide = false;                  // 穿过方块（不碰撞）
            Projectile.usesLocalNPCImmunity = true;          // 使用局部NPC免疫
            Projectile.localNPCHitCooldown = 20;             // 命中同一NPC的冷却时间20帧
            Projectile.penetrate = 8;                        // 能穿透8个敌人
            //Projectile.stopsDealingDamageAfterPenetrateHits = true;  // 穿透次数用完后停止造成伤害
        }

        public override void AI()
        {
            // ── 追踪逻辑（幻影箭风格）──
            // ai[0] 是命中后的不追踪冷却，让弹体自然穿过目标
            if (Projectile.ai[0] > 0)
            {
                Projectile.ai[0]--;
                // 冷却期间强制保持速度不衰减，直飞穿过去
                float currentSpeed = Projectile.velocity.Length();
                if (currentSpeed < 10f)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 14f;
            }
            else
            {
                int targetIndex = FindNearestTargetNotOnCooldown(5000f);
                if (targetIndex >= 0)
                {
                    NPC target = Main.npc[targetIndex];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float dist = toTarget.Length();

                    if (dist > 1f)
                    {
                        toTarget.Normalize();

                        const float desiredSpeed = 14f;
                        const float lerpAmount = 0.1f;

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
                else if (Projectile.velocity.Length() < 6f)
                {
                    Projectile.velocity *= 1.02f;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            // ── 红色尾迹粉尘 ──
            if (Main.rand.NextBool(3))  // 每帧 1/3 概率生成，稀疏一些
            {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.RedTorch, 0f, 0f, 150, default, 0.4f);  // alpha 150 更透明，scale 0.4f 更小
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.1f;  // 速度更低，散开幅度更小
            }
        }

        // 命中后进入不追踪冷却，让弹体自然穿过
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Projectile.ai[0] = 18f;

            // 命中位置生成日耀爆炸
            // ai[1] 存上一次爆炸的目标 whoAmI（+1偏移避免0的歧义）
            if (Projectile.ai[1] != target.whoAmI + 1)
            {
                Projectile.ai[1] = target.whoAmI + 1;

                if (Main.myPlayer == Projectile.owner)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        target.Center,
                        Vector2.Zero,
                        ProjectileID.DaybreakExplosion,
                        Projectile.damage,
                        0f,
                        Projectile.owner);
                }
            }

        }

        /// <summary>
        /// 找最近可追踪敌人，跳过还在无敌帧里的（幻影箭同款）
        /// </summary>
        private int FindNearestTargetNotOnCooldown(float maxRange)
        {
            int bestIndex = -1;
            float bestDistSq = maxRange * maxRange;

            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                if (Projectile.localNPCImmunity[i] > 0) continue;

                float distSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);

            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];
            for (int i = trailLen - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - i / (float)trailLen;

                Color outer = Color.DarkRed * fade * 0.8f;
                float outerScale = 14f * fade;
                Main.spriteBatch.Draw(pixel, drawPos, src, outer, 0f, new Vector2(0.5f), outerScale, SpriteEffects.None, 0f);

                Color inner = Color.OrangeRed * fade;
                float innerScale = 8f * fade;
                Main.spriteBatch.Draw(pixel, drawPos, src, inner, 0f, new Vector2(0.5f), innerScale, SpriteEffects.None, 0f);
            }

            Vector2 corePos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(pixel, corePos, src, Color.Lerp(Color.OrangeRed, Color.White, 0.5f),
                0f, new Vector2(0.5f), 12f, SpriteEffects.None, 0f);

            return false;
        }
    }
}