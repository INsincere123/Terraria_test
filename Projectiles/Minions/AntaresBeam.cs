using System;
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
            ProjectileID.Sets.TrailCacheLength[Type] = 32;  // 32个点构成火焰尾迹
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

            // ── 火焰粒子：火星飞溅 + 向后喷射的火焰粉尘 ──
            if (Main.rand.NextBool(2))  // 更频繁的火焰粒子
            {
                // Torch 粒子（主火焰）
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Torch, 0f, 0f, 100, default, Main.rand.NextFloat(0.8f, 1.3f));
                Main.dust[d].noGravity = true;
                // 向后喷射，加一点随机扰动模拟火焰飘动
                Main.dust[d].velocity = -Projectile.velocity * 0.3f
                    + new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f));
            }
            if (Main.rand.NextBool(4))
            {
                // 小概率生成亮红的 RedTorch 增加层次
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.RedTorch, 0f, 0f, 120, default, Main.rand.NextFloat(0.6f, 1f));
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = -Projectile.velocity * 0.2f
                    + new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
            }

            Lighting.AddLight(Projectile.Center, 0.8f, 0.35f, 0.1f);  // 火焰光照偏橙
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

            // 全局时间用于火焰抖动
            float globalTime = Main.GlobalTimeWrappedHourly * 8f;

            // ── 切换到 Additive 混合模式，颜色叠加发光，消除颗粒感 ──
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix);

            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];
            for (int i = trailLen - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = i / (float)trailLen;  // 0=最新, 1=最老
                float fade = 1f - progress;

                // 火焰抖动：每个点加一个正弦偏移，模拟火焰飘动
                Vector2 perpendicular = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                float wobble = MathF.Sin(globalTime + i * 0.4f) * progress * 3f;  // 越靠后抖动越大
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f
                                  + perpendicular * wobble
                                  - Main.screenPosition;

                // ── 火焰三层颜色：由外到内 暗红→亮红→橙黄→白 ──
                // 火焰温度随距离衰减：越靠后越暗越红，越靠前越亮越黄
                float temperature = fade;  // 温度参数

                // 最外层：暗红色大光晕（火焰外焰）
                Color outerFlame = Color.Lerp(new Color(80, 0, 0), new Color(200, 50, 0), temperature) * fade * 0.5f;
                float outerScale = (10f + progress * 4f) * fade;  // 越靠后越粗（火焰扩散）
                Main.spriteBatch.Draw(pixel, drawPos, src, outerFlame, 0f, new Vector2(0.5f), outerScale, SpriteEffects.None, 0f);

                // 中层：橙红色（火焰主体）
                Color midFlame = Color.Lerp(new Color(200, 60, 10), new Color(255, 140, 20), temperature) * fade * 0.75f;
                float midScale = (6f + progress * 2f) * fade;
                Main.spriteBatch.Draw(pixel, drawPos, src, midFlame, 0f, new Vector2(0.5f), midScale, SpriteEffects.None, 0f);

                // 内层：黄橙色（火焰内焰）
                Color innerFlame = Color.Lerp(new Color(255, 150, 30), new Color(255, 230, 100), temperature) * fade;
                float innerScale = 3f * fade;
                Main.spriteBatch.Draw(pixel, drawPos, src, innerFlame, 0f, new Vector2(0.5f), innerScale, SpriteEffects.None, 0f);

                // 最内层：白热高光（只在前半段尾迹）
                if (progress < 0.5f)
                {
                    Color whiteHot = Color.White * fade * (1f - progress * 2f) * 0.8f;
                    float whiteScale = 1.5f * fade;
                    Main.spriteBatch.Draw(pixel, drawPos, src, whiteHot, 0f, new Vector2(0.5f), whiteScale, SpriteEffects.None, 0f);
                }
            }

            // 弹体核心：白热四层，带脉动
            Vector2 corePos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + MathF.Sin(globalTime * 2f) * 0.15f;  // 脉动系数

            Main.spriteBatch.Draw(pixel, corePos, src, new Color(150, 30, 0) * 0.8f,
                0f, new Vector2(0.5f), 14f * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, corePos, src, new Color(255, 100, 20),
                0f, new Vector2(0.5f), 9f * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, corePos, src, new Color(255, 220, 80),
                0f, new Vector2(0.5f), 5f * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, corePos, src, Color.White,
                0f, new Vector2(0.5f), 2.5f * pulse, SpriteEffects.None, 0f);

            // ── 恢复原版 AlphaBlend 混合模式 ──
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
