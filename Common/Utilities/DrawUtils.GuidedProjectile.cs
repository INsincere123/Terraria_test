using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace TestMod.Common.Utilities
{
    public readonly struct GuidedProjectileTrailLayer
    {
        public readonly float Width;
        public readonly float WidthExponent;
        public readonly float Opacity;
        public readonly float TimeOffset;
        public readonly Func<float, float, float, Color> ColorFunction;

        public GuidedProjectileTrailLayer(
            float width,
            float widthExponent,
            float opacity,
            Func<float, float, float, Color> colorFunction,
            float timeOffset = 0f)
        {
            Width = width;
            WidthExponent = widthExponent;
            Opacity = opacity;
            ColorFunction = colorFunction;
            TimeOffset = timeOffset;
        }
    }

    public sealed class GuidedProjectileDrawSettings
    {
        public int RenderedTrailPositions = 12;
        public int CurveSamplesPerSegment = 4;
        public int PrimitivePointsPerSegment = 8;
        public float WobbleAmplitude = 1.65f;
        public float WobbleFrequency = 0.4f;
        public float TimeMultiplier = 8f;
        public string ShaderName;
        public Action<ManagedShader> ConfigureShader;
        public GuidedProjectileTrailLayer ShaderLayer;
        public GuidedProjectileTrailLayer ShaderFallbackLayer;
        public GuidedProjectileTrailLayer[] AdditionalLayers = [];
        public Texture2D HeadTexture;
        public float HeadScale = 1f;
        public float HeadPulseAmplitude = 0.08f;
        public float HeadPulseFrequency = 16f;
        public Action<Projectile, float> FallbackHeadDrawer;
    }

    public static partial class DrawUtils
    {
        public static void DrawGuidedProjectile(Projectile projectile, GuidedProjectileDrawSettings settings)
        {
            if (settings is null)
                return;

            float visualTime = Main.GlobalTimeWrappedHourly * settings.TimeMultiplier;
            TrailDrawBuffer buffer = RentTrailBuffer();
            try
            {
                List<Vector2> trailPoints = CreateGuidedProjectileTrailPoints(projectile, settings, visualTime, buffer);
                if (trailPoints.Count >= 2)
                    DrawGuidedProjectileTrail(trailPoints, settings, visualTime, buffer);
                else
                    Main.spriteBatch.End();

                DrawGuidedProjectileHead(projectile, settings, visualTime);
            }
            finally { ReturnTrailBuffer(buffer); }
        }

        private static List<Vector2> CreateGuidedProjectileTrailPoints(Projectile projectile, GuidedProjectileDrawSettings settings, float visualTime, TrailDrawBuffer buffer)
        {
            List<Vector2> points = buffer.Points;
            int trailLen = Math.Min(settings.RenderedTrailPositions, projectile.oldPos.Length);
            Vector2 perpendicular = projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);

            for (int i = trailLen - 1; i >= 0; i--)
            {
                if (projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = i / (float)Math.Max(trailLen - 1, 1);
                float wobble = MathF.Sin(visualTime + i * settings.WobbleFrequency) * progress * settings.WobbleAmplitude;
                points.Add(projectile.oldPos[i] + projectile.Size * 0.5f + perpendicular * wobble);
            }

            points.Add(projectile.Center);
            return SmoothGuidedProjectileTrail(points, settings.CurveSamplesPerSegment, buffer.Smoothed);
        }

        private static List<Vector2> SmoothGuidedProjectileTrail(List<Vector2> points, int samplesPerSegment, List<Vector2> smoothed)
        {
            if (points.Count <= 2 || samplesPerSegment <= 1)
                return points;

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p0 = points[Math.Max(i - 1, 0)];
                Vector2 p1 = points[i];
                Vector2 p2 = points[i + 1];
                Vector2 p3 = points[Math.Min(i + 2, points.Count - 1)];

                for (int j = 0; j < samplesPerSegment; j++)
                {
                    float amount = j / (float)samplesPerSegment;
                    smoothed.Add(Vector2.CatmullRom(p0, p1, p2, p3, amount));
                }
            }

            smoothed.Add(points[^1]);
            return smoothed;
        }

        private static void DrawGuidedProjectileTrail(List<Vector2> points, GuidedProjectileDrawSettings settings, float visualTime, TrailDrawBuffer buffer)
        {
            PrepareForAdditivePrimitives(Main.spriteBatch);

            if (!string.IsNullOrWhiteSpace(settings.ShaderName) && ShaderManager.TryGetShader(settings.ShaderName, out ManagedShader shader))
            {
                settings.ConfigureShader?.Invoke(shader);
                DrawGuidedProjectileTrailLayer(points, settings.ShaderLayer, settings.PrimitivePointsPerSegment, visualTime, shader, buffer);
            }
            else
            {
                DrawGuidedProjectileTrailLayer(points, settings.ShaderFallbackLayer, settings.PrimitivePointsPerSegment, visualTime, null, buffer);
            }

            foreach (GuidedProjectileTrailLayer layer in settings.AdditionalLayers)
                DrawGuidedProjectileTrailLayer(points, layer, settings.PrimitivePointsPerSegment, visualTime, null, buffer);
        }

        private static void DrawGuidedProjectileTrailLayer(List<Vector2> points, GuidedProjectileTrailLayer layer, int pointsPerSegment, float visualTime, ManagedShader shader, TrailDrawBuffer buffer)
        {
            if (layer.ColorFunction is null || layer.Width <= 0f || pointsPerSegment <= 0)
                return;

            buffer.Style.Guided = true;
            buffer.Style.Layer = layer;
            buffer.Style.VisualTime = visualTime;
            PrimitiveRenderer.RenderTrail(points, buffer.Style.Settings(shader: shader), pointsPerSegment);
        }

        private static void DrawGuidedProjectileHead(Projectile projectile, GuidedProjectileDrawSettings settings, float visualTime)
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix);

            float pulse = 1f + MathF.Sin(visualTime * settings.HeadPulseFrequency / Math.Max(settings.TimeMultiplier, 0.001f)) * settings.HeadPulseAmplitude;

            if (settings.HeadTexture is not null)
            {
                Vector2 origin = settings.HeadTexture.Size() * 0.5f;
                Main.spriteBatch.Draw(
                    settings.HeadTexture,
                    projectile.Center - Main.screenPosition,
                    null,
                    Color.White,
                    projectile.rotation,
                    origin,
                    settings.HeadScale * pulse,
                    SpriteEffects.None,
                    0f);
            }
            else if (settings.FallbackHeadDrawer is not null)
            {
                settings.FallbackHeadDrawer(projectile, pulse);
            }

            Main.spriteBatch.End();
        }

        private static float GuidedProjectileWidth(float completion, float baseWidth, float exponent)
        {
            float headToTail = MathHelper.SmoothStep(0.08f, 1f, completion);
            float tailFloor = MathHelper.Lerp(0.08f, 1f, headToTail);
            return baseWidth * MathF.Pow(tailFloor, exponent);
        }

        public static void DrawGuidedProjectileDiamondFallback(Projectile projectile, float pulse)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 corePos = projectile.Center - Main.screenPosition;

            Main.spriteBatch.Draw(pixel, corePos, src, new Color(255, 92, 18) * 0.55f,
                projectile.rotation + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(18f, 9f) * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, corePos, src, new Color(255, 210, 90) * 0.8f,
                projectile.rotation + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(10f, 5f) * pulse, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, corePos, src, Color.White * 0.8f,
                projectile.rotation + MathHelper.PiOver4, new Vector2(0.5f), new Vector2(4.5f, 2.2f) * pulse, SpriteEffects.None, 0f);
        }
    }
}
