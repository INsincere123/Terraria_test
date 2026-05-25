using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;

namespace TestMod.Content.Projectiles.Minions
{
    public class AntaresBeam : ModProjectile
    {
        private const string BeamShaderName = "TestMod.AntaresBeamShader";
        private const int RenderedTrailPositions = 12;
        private const int CurveSamplesPerSegment = 6;
        private const int PrimitivePointsPerSegment = 9;

        private static readonly GuidedProjectileTrailLayer[] TrailLayers =
        [
            new GuidedProjectileTrailLayer(3.8f, 0.92f, 0.42f, BeamColor, 1.7f),
            new GuidedProjectileTrailLayer(1.25f, 0.75f, 0.9f, (completion, time, opacity) => BeamCoreColor(completion, time) * opacity)
        ];

        private static readonly GuidedProjectileDrawSettings DrawSettings = new()
        {
            RenderedTrailPositions = RenderedTrailPositions,
            CurveSamplesPerSegment = CurveSamplesPerSegment,
            PrimitivePointsPerSegment = PrimitivePointsPerSegment,
            WobbleAmplitude = 1.65f,
            WobbleFrequency = 0.4f,
            TimeMultiplier = 8f,
            ShaderName = BeamShaderName,
            ConfigureShader = shader =>
            {
                shader.TrySetParameter("globalTime", Main.GlobalTimeWrappedHourly);
                shader.TrySetParameter("beamIntensity", 0.86f);
                shader.TrySetParameter("noisePresence", AntaresVisualAssetSystem.HasHaloNoise ? 1f : 0f);
                shader.SetTexture(AntaresVisualAssetSystem.HaloNoise, 2, SamplerState.LinearWrap);
            },
            ShaderLayer = new GuidedProjectileTrailLayer(8.5f, 1.05f, 0.76f, BeamColor),
            ShaderFallbackLayer = new GuidedProjectileTrailLayer(9.5f, 1.08f, 0.15f, BeamColor),
            AdditionalLayers = TrailLayers,
            HeadScale = 1.58f,
            HeadPulseAmplitude = 0.08f,
            HeadPulseFrequency = 16f,
            FallbackHeadDrawer = DrawUtils.DrawGuidedProjectileDiamondFallback
        };

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
            // ai[0] is the post-hit non-homing cooldown so the shot can pass through naturally.
            if (Projectile.ai[0] > 0)
            {
                Projectile.ai[0]--;

                float currentSpeed = Projectile.velocity.Length();
                if (currentSpeed < 10f)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 14f;
            }
            else
            {
                int targetIndex = TargetUtils.FindNearestTargetNotOnCooldown(Projectile.Center, 5000f);
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

            Lighting.AddLight(Projectile.Center, 0.8f, 0.35f, 0.1f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Projectile.ai[0] = 18f;

            // ai[1] stores the last exploded target as whoAmI + 1, avoiding the ambiguity of 0.
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

        public override bool PreDraw(ref Color lightColor)
        {
            DrawUtils.DrawGuidedProjectile(Projectile, CreateDrawSettings());

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

        private static GuidedProjectileDrawSettings CreateDrawSettings()
        {
            DrawSettings.HeadTexture = AntaresVisualAssetSystem.HasAntaresBeamHead ? AntaresVisualAssetSystem.AntaresBeamHead : null;
            return DrawSettings;
        }

        private static Color BeamColor(float completion, float globalTime, float opacity)
        {
            float heat = MathHelper.Clamp(completion * 1.18f, 0f, 1f);
            float flicker = 0.86f + MathF.Sin(globalTime + completion * 10.5f) * 0.11f;
            Color ember = Color.Lerp(new Color(95, 8, 0), new Color(255, 78, 16), heat);
            Color gold = Color.Lerp(ember, new Color(255, 185, 64), heat * heat);
            return gold * (opacity * flicker);
        }

        private static Color BeamCoreColor(float completion, float globalTime)
        {
            float fade = MathF.Sin(completion * MathHelper.Pi);
            float headHeat = MathHelper.SmoothStep(0.54f, 1f, completion);
            float flicker = 0.9f + MathF.Sin(globalTime * 1.35f + completion * 17f) * 0.1f;
            Color color = Color.Lerp(new Color(255, 182, 72), Color.White, headHeat);
            return color * (fade * flicker * 0.9f);
        }
    }
}
