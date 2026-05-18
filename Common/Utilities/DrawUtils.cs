using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace TestMod.Common.Utilities
{
    public readonly struct SpriteBatchSettings
    {
        public readonly BlendState BlendState;
        public readonly SamplerState SamplerState;
        public readonly DepthStencilState DepthStencilState;
        public readonly RasterizerState RasterizerState;

        public SpriteBatchSettings(BlendState blendState, SamplerState samplerState, DepthStencilState depthStencilState, RasterizerState rasterizerState)
        {
            BlendState = blendState;
            SamplerState = samplerState;
            DepthStencilState = depthStencilState;
            RasterizerState = rasterizerState;
        }

        public static readonly SpriteBatchSettings AlphaBlend = new(BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, null);
        public static readonly SpriteBatchSettings Additive = new(BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, null);
        public static readonly SpriteBatchSettings AdditiveLinearClamp = new(BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        public static readonly SpriteBatchSettings AlphaBlendLinearClamp = new(BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        public static readonly SpriteBatchSettings NonPremultiplied = new(BlendState.NonPremultiplied, Main.DefaultSamplerState, DepthStencilState.None, null);
        public static readonly SpriteBatchSettings Opaque = new(BlendState.Opaque, Main.DefaultSamplerState, DepthStencilState.None, null);
    }

    public readonly struct PixelStarSettings
    {
        public readonly Color CoreColor;
        public readonly Color GlowColor;
        public readonly Color SpikeColor;
        public readonly float Size;
        public readonly float Brightness;
        public readonly float VisualScale;
        public readonly bool DrawSpikes;
        public readonly bool LongSpikes;

        public PixelStarSettings(
            Color coreColor,
            Color glowColor,
            Color spikeColor,
            float size,
            float brightness,
            float visualScale = 1f,
            bool drawSpikes = false,
            bool longSpikes = false)
        {
            CoreColor = coreColor;
            GlowColor = glowColor;
            SpikeColor = spikeColor;
            Size = size;
            Brightness = brightness;
            VisualScale = visualScale;
            DrawSpikes = drawSpikes;
            LongSpikes = longSpikes;
        }
    }

    public static partial class DrawUtils
    {
        public delegate void ChromaAberrationDelegate(Vector2 offset, Color colorMultiplier);

        public static void RestartSpriteBatch(
            this SpriteBatch spriteBatch,
            SpriteSortMode sortMode,
            SpriteBatchSettings settings,
            Effect effect = null,
            Matrix? matrix = null)
        {
            spriteBatch.End();
            spriteBatch.Begin(
                sortMode,
                settings.BlendState,
                settings.SamplerState,
                settings.DepthStencilState,
                settings.RasterizerState ?? Main.Rasterizer,
                effect,
                matrix ?? Main.GameViewMatrix.TransformationMatrix);
        }

        public static void BeginSpriteBatch(
            this SpriteBatch spriteBatch,
            SpriteSortMode sortMode,
            SpriteBatchSettings settings,
            Effect effect = null,
            Matrix? matrix = null)
        {
            spriteBatch.Begin(
                sortMode,
                settings.BlendState,
                settings.SamplerState,
                settings.DepthStencilState,
                settings.RasterizerState ?? Main.Rasterizer,
                effect,
                matrix ?? Main.GameViewMatrix.TransformationMatrix);
        }

        public static void SetBlendState(this SpriteBatch spriteBatch, BlendState blendState)
        {
            spriteBatch.RestartSpriteBatch(
                SpriteSortMode.Immediate,
                new SpriteBatchSettings(blendState, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer));
        }

        public static void BeginAdditive(SpriteBatch spriteBatch)
        {
            spriteBatch.RestartSpriteBatch(SpriteSortMode.Immediate, SpriteBatchSettings.Additive);
        }

        public static void EndAdditive(SpriteBatch spriteBatch)
        {
            spriteBatch.RestartSpriteBatch(SpriteSortMode.Deferred, SpriteBatchSettings.AlphaBlend);
        }

        public static void PrepareForAdditivePrimitives(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
            Main.instance.GraphicsDevice.BlendState = BlendState.Additive;
        }

        public static void EnterShaderRegion(this SpriteBatch spriteBatch, BlendState blendState = null, Effect effect = null, Matrix? matrix = null)
        {
            spriteBatch.RestartSpriteBatch(
                SpriteSortMode.Immediate,
                new SpriteBatchSettings(blendState ?? BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer),
                effect,
                matrix);
        }

        public static void ExitShaderRegion(this SpriteBatch spriteBatch, Matrix? matrix = null)
        {
            spriteBatch.RestartSpriteBatch(SpriteSortMode.Deferred, SpriteBatchSettings.AlphaBlend, matrix: matrix);
        }

        public static void EnforceCutoffRegion(this SpriteBatch spriteBatch, Rectangle cutoffRegion, Matrix perspective, SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = null)
        {
            RasterizerState rasterizer = Main.Rasterizer;
            rasterizer.ScissorTestEnable = true;

            spriteBatch.End();
            spriteBatch.Begin(sortMode, blendState ?? BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, rasterizer, null, perspective);
            spriteBatch.GraphicsDevice.ScissorRectangle = cutoffRegion;
        }

        public static void ReleaseCutoffRegion(this SpriteBatch spriteBatch, Matrix perspective, SpriteSortMode sortMode = SpriteSortMode.Deferred)
        {
            int width = spriteBatch.GraphicsDevice.Viewport.Width;
            int height = spriteBatch.GraphicsDevice.Viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(sortMode, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, perspective);
            spriteBatch.GraphicsDevice.ScissorRectangle = new Rectangle(-1, -1, width + 2, height + 2);
        }

        public static void DrawOutline(
            SpriteBatch spriteBatch,
            Texture2D texture,
            Vector2 position,
            float rotation,
            float scale,
            Vector2 origin,
            Color color,
            float thickness = 2f)
        {
            spriteBatch.Draw(texture, position + new Vector2(thickness, 0f), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, position + new Vector2(-thickness, 0f), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, position + new Vector2(0f, thickness), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, position + new Vector2(0f, -thickness), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }

        public static void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float width = 1f)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 direction = end - start;
            float length = direction.Length();

            if (length <= 0f)
                return;

            spriteBatch.Draw(
                pixel,
                start - Main.screenPosition,
                new Rectangle(0, 0, 1, 1),
                color,
                direction.ToRotation(),
                new Vector2(0f, 0.5f),
                new Vector2(length, width),
                SpriteEffects.None,
                0f);
        }

        public static void DrawLineBetweenPoints(IReadOnlyList<Vector2> points, Color color, bool useTileColor = false, float width = 1f)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Color drawColor = useTileColor ? Lighting.GetColor(points[i].ToTileCoordinates(), color) : color;
                DrawLine(Main.spriteBatch, points[i], points[i + 1], drawColor, width);
            }
        }

        public static void SpawnDustLine(Vector2 from, Vector2 to, Color color, int dustType = DustID.Electric, int count = 6, float scale = 1.1f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            Dust.QuickDustLine(from, to, 10f, color);

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                Vector2 pos = Vector2.Lerp(from, to, t);
                Dust.NewDustPerfect(pos, dustType, Vector2.Zero, 0, color, scale);
            }

            Dust.NewDustPerfect(to, dustType, Vector2.Zero, 0, Color.White, scale + 0.3f);
        }

        public static Color ColorSwap(Color firstColor, Color secondColor, float seconds)
        {
            double timeMultiplier = MathHelper.TwoPi / seconds;
            float interpolant = (float)((Math.Sin(timeMultiplier * Main.GlobalTimeWrappedHourly) + 1D) * 0.5D);
            return Color.Lerp(firstColor, secondColor, interpolant);
        }

        public static Color MulticolorLerp(float increment, params Color[] colors)
        {
            if (colors.Length == 0)
                return Color.White;
            if (colors.Length == 1)
                return colors[0];

            increment %= 0.999f;
            if (increment < 0f)
                increment += 0.999f;

            int currentColorIndex = (int)(increment * colors.Length);
            Color currentColor = colors[currentColorIndex];
            Color nextColor = colors[(currentColorIndex + 1) % colors.Length];
            return Color.Lerp(currentColor, nextColor, increment * colors.Length % 1f);
        }

        public static void DrawChromaticAberration(Vector2 direction, float strength, ChromaAberrationDelegate drawCall)
        {
            for (int i = -1; i <= 1; i++)
            {
                Color aberrationColor = i switch
                {
                    -1 => new Color(255, 0, 0, 0),
                    0 => new Color(0, 255, 0, 0),
                    _ => new Color(0, 0, 255, 0),
                };

                Vector2 offset = direction.RotatedBy(MathHelper.PiOver2) * i * strength;
                drawCall.Invoke(offset, aberrationColor);
            }
        }

        public static Vector2[] CreateWavyLinePoints(Vector2 start, Vector2 end, int pointCount, float waveAmplitude, float phase)
        {
            pointCount = Math.Max(pointCount, 2);
            Vector2 delta = end - start;
            Vector2 normal = new Vector2(-delta.Y, delta.X).SafeNormalize(Vector2.Zero);
            Vector2[] points = new Vector2[pointCount];

            for (int i = 0; i < points.Length; i++)
            {
                float completion = i / (float)(points.Length - 1);
                float wave = MathF.Sin(completion * MathHelper.Pi + phase) * waveAmplitude;
                points[i] = Vector2.Lerp(start, end, completion) + normal * wave;
            }

            return points;
        }

        public static void DrawPrimitiveTrail(Vector2[] points, Color color, float width, int pointsPerSegment = 14, bool smoothen = true)
        {
            PrimitiveRenderer.RenderTrail(
                points,
                new PrimitiveSettings(
                    _ => width,
                    completion =>
                    {
                        float fade = MathF.Sin(completion * MathHelper.Pi);
                        return color * MathHelper.Clamp(fade * 1.18f, 0f, 1f);
                    },
                    Smoothen: smoothen),
                pointsPerSegment);
        }

        public static void DrawConstellationLine(Vector2 start, Vector2 end, float weight, int tier, float time)
        {
            float shimmer = 0.72f + MathF.Sin(time * (1.15f + tier * 0.2f) + start.X * 0.01f) * 0.16f;
            float tierGlow = 1f + tier * 0.3f;
            Color outer = Color.Lerp(new Color(45, 120, 255), new Color(255, 70, 22), tier / 3f);
            Color inner = Color.Lerp(new Color(180, 235, 255), new Color(255, 205, 95), tier / 3f);
            Vector2[] points = CreateWavyLinePoints(start, end, 6, (0.65f + tier * 0.28f) * weight, time * 0.55f + start.X * 0.006f);

            DrawPrimitiveTrail(points, outer * (0.17f * shimmer), (3.8f + tier * 1.15f) * weight * tierGlow);
            DrawPrimitiveTrail(points, outer * (0.28f * shimmer), (1.9f + tier * 0.45f) * weight);
            DrawPrimitiveTrail(points, inner * (0.58f * shimmer), (0.72f + tier * 0.11f) * weight);
        }

        public static void DrawEnergyPulse(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float weight, int tier, float time)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            Vector2 point = Vector2.Lerp(start, end, time * (0.18f + tier * 0.03f) % 1f) - Main.screenPosition;
            float pulse = 1f + MathF.Sin(time * 6f) * 0.18f;
            Color color = tier >= 3 ? new Color(255, 190, 70) : new Color(140, 220, 255);

            spriteBatch.Draw(pixel, point, source, color * 0.35f, 0f, new Vector2(0.5f), 11f * weight * pulse, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, point, source, Color.White * 0.78f, 0f, new Vector2(0.5f), 3.2f * weight * pulse, SpriteEffects.None, 0f);
        }

        public static void DrawPixelStar(SpriteBatch spriteBatch, Vector2 worldPosition, PixelStarSettings settings, int tier, float time, int seed)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            Vector2 position = worldPosition - Main.screenPosition;
            float twinkle = 0.88f + MathF.Sin(time * (1.8f + seed * 0.11f) + seed * 1.37f) * 0.18f;
            float tierScale = 1f + tier * 0.18f;
            float scale = settings.Size * settings.Brightness * twinkle * tierScale * settings.VisualScale;

            spriteBatch.Draw(pixel, position, source, settings.GlowColor * 0.16f, 0f, new Vector2(0.5f), 20f * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, position, source, settings.GlowColor * 0.28f, 0f, new Vector2(0.5f), 11f * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, position, source, settings.CoreColor * 0.85f, 0f, new Vector2(0.5f), 4.2f * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, position, source, Color.White * 0.9f, 0f, new Vector2(0.5f), 1.55f * scale, SpriteEffects.None, 0f);

            if (settings.DrawSpikes)
                DrawStarSpikes(spriteBatch, position, settings.SpikeColor, scale, tier, time, settings.LongSpikes);
        }

        public static bool TryDrawCenteredShaderQuad(string shaderName, Texture2D texture, Vector2 worldCenter, Vector2 size, Action<ManagedShader> configureShader = null)
        {
            if (!ShaderManager.TryGetShader(shaderName, out ManagedShader shader))
                return false;

            configureShader?.Invoke(shader);

            Vector2 quadAnchor = worldCenter - new Vector2(size.X * 0.5f, -size.Y * 0.5f);
            PrimitiveRenderer.RenderQuad(texture, quadAnchor, size, 0f, Color.White, shader);
            return true;
        }

        public static void DrawStarSpikes(SpriteBatch spriteBatch, Vector2 screenPosition, Color color, float scale, int tier, float time, bool longSpikes)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            float rotation = time * (longSpikes ? 0.4f : -0.25f);
            float length = (longSpikes ? 28f : 17f) * (1f + tier * 0.18f) * scale;
            float width = (longSpikes ? 1.7f : 1.1f) * scale;

            for (int i = 0; i < 4; i++)
            {
                float angle = rotation + MathHelper.PiOver2 * i;
                DrawSpike(spriteBatch, pixel, source, screenPosition, angle, length, width, color, 0.42f);
                DrawSpike(spriteBatch, pixel, source, screenPosition, angle + MathHelper.PiOver4, length * 0.55f, width * 0.75f, color, 0.24f);
            }
        }

        private static void DrawSpike(SpriteBatch spriteBatch, Texture2D pixel, Rectangle source, Vector2 position, float angle, float length, float width, Color color, float alpha)
        {
            spriteBatch.Draw(pixel, position, source, color * alpha, angle, new Vector2(0f, 0.5f), new Vector2(length, width), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, position, source, color * alpha, angle + MathHelper.Pi, new Vector2(0f, 0.5f), new Vector2(length, width), SpriteEffects.None, 0f);
        }
    }
}
