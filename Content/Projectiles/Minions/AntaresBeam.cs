using System;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;
using ProjectileTracking = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Content.Projectiles.Minions
{
    public class AntaresBeam : ModProjectile
    {
        private bool visualSpawnPlayed;
        private float lastObservedHitCooldown;

        private const string BeamShaderName = "TestMod.AntaresBeamShader";
        private const int RenderedTrailPositions = 12;
        private const int CurveSamplesPerSegment = 4;
        // RenderTrail 的第三参数是整条路径的采样数，不是每个控制段的采样数。
        // 末端还会略去最后一组索引，64点把可见末端收至弹头纹理内部。
        private const int PrimitiveTrailSamples = 64;

        // 生成时由owner写入，沿原有弹幕ai同步；ai[0]仍是命中冷却，ai[1]不再用于爆炸。
        private bool IsBurst => Projectile.ai[2] == 1f;
        private static readonly GuidedProjectileDrawSettings NormalDrawSettings = CreateDrawSettings(false);
        private static readonly GuidedProjectileDrawSettings BurstDrawSettings = CreateDrawSettings(true);

        private static GuidedProjectileDrawSettings CreateDrawSettings(bool burst)
        {
            Func<float, float, float, Color> color = burst ? BurstColor : BeamColor;
            return new GuidedProjectileDrawSettings
            {
                RenderedTrailPositions = RenderedTrailPositions,
                CurveSamplesPerSegment = CurveSamplesPerSegment,
                PrimitivePointsPerSegment = PrimitiveTrailSamples,
                WobbleAmplitude = 0.65f,
                WobbleFrequency = 0.4f,
                TimeMultiplier = 8f,
                ShaderName = BeamShaderName,
                ConfigureShader = shader =>
                {
                    shader.TrySetParameter("globalTime", Main.GlobalTimeWrappedHourly);
                    shader.TrySetParameter("beamIntensity", 0.85f);
                    shader.TrySetParameter("burstPalette", burst ? 1f : 0f);
                    shader.TrySetParameter("noisePresence", AntaresVisualAssetSystem.HasHaloNoise ? 1f : 0f);
                    shader.SetTexture(AntaresVisualAssetSystem.HaloNoise, 2, SamplerState.LinearWrap);
                    shader.TrySetParameter("materialPresence", new Vector3(
                        QuasarJetVisualAssetSystem.HasBeamBodyTexture ? 1f : 0f,
                        QuasarJetVisualAssetSystem.HasBeamFlowTexture ? 1f : 0f,
                        QuasarJetVisualAssetSystem.HasBeamCoreTexture ? 1f : 0f));
                    shader.SetTexture(QuasarJetVisualAssetSystem.BeamBodyTexture, 3, SamplerState.LinearClamp);
                    shader.SetTexture(QuasarJetVisualAssetSystem.BeamFlowTexture, 4, SamplerState.LinearClamp);
                    shader.SetTexture(QuasarJetVisualAssetSystem.BeamCoreTexture, 5, SamplerState.LinearClamp);
                },
                // 材质shader内统一着色，顶点保持中性，防止纹理配色被重复相乘。
                ShaderLayer = new GuidedProjectileTrailLayer(5.8f, 1.05f, 1f, NeutralColor),
                ShaderFallbackLayer = new GuidedProjectileTrailLayer(6.5f, 1.08f, 0.15f, color),
                HeadScale = 0.90f,
                HeadPulseAmplitude = 0.025f,
                HeadPulseFrequency = 8f,
                FallbackHeadDrawer = burst ? DrawBurstHead : DrawNormalHead
            };
        }

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 32;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.timeLeft = 200;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.MaxUpdates = 2;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.penetrate = 9;
            Projectile.stopsDealingDamageAfterPenetrateHits = true;
        }

        public override void AI()
        {
            if (!Main.dedServ)
            {
                // 远端从既有冷却同步推断命中，不新增视觉包；本地命中钩子会预先记录18。
                if (Projectile.ai[0] > lastObservedHitCooldown)
                    NotifyImpact();
                lastObservedHitCooldown = Projectile.ai[0];
            }
            if (!Main.dedServ && !visualSpawnPlayed)
            {
                visualSpawnPlayed = true;
                // 仅刚出现且靠近本体的光束触发反馈，复用已有网络同步。
                if (Projectile.timeLeft >= 196)
                {
                    foreach (Projectile minion in ProjectileLookup.Owned(Projectile.owner, ModContent.ProjectileType<AntaresMinion>()))
                    {
                        if (Vector2.DistanceSquared(minion.Center, Projectile.Center) <= 80f * 80f &&
                            minion.ModProjectile is AntaresMinion antares)
                            antares.NotifyShotVisual(IsBurst);
                    }
                }
            }
            // ai[0] is the post-hit non-homing cooldown so the shot can pass through naturally.
            if (Projectile.ai[0] > 0)
            {
                Projectile.ai[0]--;

                float currentSpeed = Projectile.velocity.Length();
                if (currentSpeed < 11.5f)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 16.1f;
            }
            else
            {
                int targetIndex = ProjectileTracking.FindTrackingTarget(Projectile, Projectile.Center,
                    5000f, prioritizeMinionTarget: true);
                if (targetIndex >= 0)
                {
                    NPC target = Main.npc[targetIndex];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float dist = toTarget.Length();

                    if (dist > 1f)
                    {
                        toTarget.Normalize();

                        const float desiredSpeed = 16.1f;
                        const float lerpAmount = 0.125f;

                        Projectile.velocity = Vector2.Lerp(
                            Projectile.velocity,
                            toTarget * desiredSpeed,
                            lerpAmount);

                        if (Projectile.velocity.LengthSquared() < desiredSpeed * desiredSpeed * 0.64f)
                            Projectile.velocity = Projectile.velocity.SafeNormalize(toTarget) * (desiredSpeed * 0.8f);
                    }
                }
                else if (Projectile.velocity.Length() < 6f)
                {
                    Projectile.velocity *= 1.02f;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            if (!Main.dedServ)
                Lighting.AddLight(Projectile.Center, IsBurst ? new Vector3(0.20f, 0.50f, 0.85f) : new Vector3(0.8f, 0.35f, 0.1f));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Projectile.ai[0] = 18f;
            Projectile.netUpdate = true;

            if (!Main.dedServ)
            {
                NotifyImpact();
                lastObservedHitCooldown = Projectile.ai[0];
            }
        }

        private void NotifyImpact()
        {
            foreach (Projectile minion in ProjectileLookup.Owned(Projectile.owner, ModContent.ProjectileType<AntaresMinion>()))
            {
                if (minion.ModProjectile is AntaresMinion antares)
                    antares.NotifyImpactVisual(Projectile.Center, Projectile.velocity, IsBurst);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Main.dedServ)
                return false;
            GuidedProjectileDrawSettings settings = IsBurst ? BurstDrawSettings : NormalDrawSettings;
            float fade = VisualFade(Projectile);
            settings.ShaderLayer = new GuidedProjectileTrailLayer(5.8f, 1.05f, fade, NeutralColor);
            settings.ShaderFallbackLayer = new GuidedProjectileTrailLayer(6.5f, 1.08f, 0.30f * fade, IsBurst ? BurstColor : BeamColor);
            DrawUtils.DrawGuidedProjectile(Projectile, settings);

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

        private static float VisualFade(Projectile projectile) =>
            MathHelper.Clamp(projectile.timeLeft / (6f * projectile.MaxUpdates), 0f, 1f);

        private static Color NeutralColor(float completion, float time, float opacity) => new Color(1f, 1f, 1f, opacity);

        private static void DrawNormalHead(Projectile projectile, float pulse) => DrawTexturedHead(projectile, pulse, false);
        private static void DrawBurstHead(Projectile projectile, float pulse) => DrawTexturedHead(projectile, pulse, true);

        private static void DrawTexturedHead(Projectile projectile, float pulse, bool burst)
        {
            Vector2 position = projectile.Center - Main.screenPosition;
            float fade = VisualFade(projectile);
            if (AntaresVisualAssetSystem.HasAntaresBeamHead)
            {
                Texture2D head = AntaresVisualAssetSystem.AntaresBeamHead;
                // 此纹理亮核位于(40,32)，不是图片中心；以亮核对齐路径末端。
                // 压低横向柔光，让纹理尾焰与12像素以内的拖尾自然重叠。
                Vector2 origin = head.Size() * new Vector2(0.625f, 0.5f);
                Vector2 scale = new Vector2(0.90f, 0.72f) * pulse;
                if (ShaderManager.TryGetShader(BeamShaderName, out ManagedShader shader))
                {
                    // 每次显式设置配色，HeadPass与拖尾pass共享effect但不共享隐含状态。
                    shader.TrySetParameter("burstPalette", burst ? 1f : 0f);
                    shader.TrySetParameter("headOpacity", fade);
                    shader.TrySetParameter("noisePresence", AntaresVisualAssetSystem.HasHaloNoise ? 1f : 0f);
                    shader.SetTexture(AntaresVisualAssetSystem.HaloNoise, 2, SamplerState.LinearWrap);
                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                        SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                        null, Main.GameViewMatrix.TransformationMatrix);
                    try
                    {
                        shader.Apply("HeadPass");
                        Main.spriteBatch.Draw(head, position, null, Color.White, projectile.rotation,
                            origin, scale, SpriteEffects.None, 0f);
                    }
                    finally
                    {
                        Main.spriteBatch.End();
                        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                            SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                            null, Main.GameViewMatrix.TransformationMatrix);
                    }
                    return;
                }
                if (!burst)
                {
                    Main.spriteBatch.Draw(head, position, null, Color.White * fade, projectile.rotation,
                        origin, scale, SpriteEffects.None, 0f);
                    return;
                }
            }
            // 缺少纹理或蓝白重着色shader时，保留小弹头，不使用暖色纹理冒充爆发。
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            Color outer = burst ? new Color(75, 160, 255) : new Color(255, 110, 30);
            Color core = burst ? new Color(210, 238, 255) : new Color(255, 225, 170);
            Main.spriteBatch.Draw(pixel, position, source, outer * (0.65f * fade),
                projectile.rotation + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(14f, 7f) * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, position, source, core * (0.88f * fade),
                projectile.rotation + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4f, 2f) * pulse, SpriteEffects.None, 0f);
        }

        private static Color BurstColor(float completion, float globalTime, float opacity)
        {
            float heat = MathHelper.Clamp(completion * 1.18f, 0f, 1f);
            float flicker = 0.86f + MathF.Sin(globalTime + completion * 10.5f) * 0.11f;
            Color blue = Color.Lerp(new Color(16, 45, 130), new Color(75, 160, 255), heat);
            return Color.Lerp(blue, new Color(185, 225, 255), heat * heat) * (opacity * flicker);
        }

        private static Color BeamColor(float completion, float globalTime, float opacity)
        {
            float heat = MathHelper.Clamp(completion * 1.18f, 0f, 1f);
            float flicker = 0.86f + MathF.Sin(globalTime + completion * 10.5f) * 0.11f;
            Color ember = Color.Lerp(new Color(95, 8, 0), new Color(255, 78, 16), heat);
            Color gold = Color.Lerp(ember, new Color(255, 185, 64), heat * heat);
            return gold * (opacity * flicker);
        }

    }
}
