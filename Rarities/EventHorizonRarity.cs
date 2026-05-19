using System;
using System.Collections.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace TestMod.Rarities
{
    public class EventHorizonRarity : ModRarity
    {
        public const string CompiledShaderPath = "Assets/Effects/EventHorizonRarityShader.fxc";

        // ==================== 事件视界稀有度参数 ====================
        // 主体颜色：文字本体使用接近黑洞中心的深紫黑，保证可读性。
        public static readonly Color TextCoreColor = new Color(24, 10, 38);
        public static readonly Color TextInnerColor = new Color(70, 25, 105);

        // 吸积盘边缘：橙金光只做轻微描边，数值过高会影响阅读。
        public static readonly Color AccretionGold = new Color(255, 166, 55);
        public static readonly Color AccretionOrange = new Color(255, 95, 34);
        public const float EdgeGlowRadius = 1.8f;
        public const int EdgeGlowLayers = 8;
        public const float EdgeGlowOpacity = 0.34f;

        // 事件视界阴影：贴近文字的一层暗描边，让主体不会被金边吞掉。
        public const float ShadowRadius = 1.2f;
        public const int ShadowLayers = 6;

        // 局部纹理边距：给描边、粒子和透镜扭曲预留采样空间。
        public const int RenderPadding = 28;

        // 下落粒子：数量少，尺寸和亮度都压低，只做接近不可见的氛围。
        public const int FallingParticleCount = 3;
        public const float ParticleCycleSeconds = 6.4f;
        public const float ParticleVisibleSeconds = 1.25f;
        public const float ParticleFallDistance = 22f;
        public const float ParticleSideDrift = 14f;
        public const float ParticleRadius = 0.65f;
        public const float ParticleGlowRadius = 2.2f;
        public const float ParticleOpacity = 0.16f;

        // 文字透镜：独立随机出现在 tooltip 区域内，范围较小，避免整框大幅晃动。
        public const int LensSourceCount = 3;
        public const float LensCycleSeconds = 3.8f;
        public const float LensActiveSeconds = 0.75f;
        public const float TextLensRadius = 0.018f;
        public const float TextLensStrength = 4.8f;

        private static RenderTarget2D textTarget;
        private static Effect textLensShader;

        public override Color RarityColor => Color.Lerp(TextInnerColor, AccretionGold, 0.35f);

        public static void DrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines)
        {
            if (Main.dedServ || lines.Count <= 0)
                return;

            Rectangle bounds = CalculateTooltipBounds(lines);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            int targetWidth = bounds.Width + RenderPadding * 2;
            int targetHeight = bounds.Height + RenderPadding * 2;
            EnsureRenderTarget(targetWidth, targetHeight);

            float time = Main.GlobalTimeWrappedHourly;
            Vector2 tooltipSize = bounds.Size();
            Vector2[] particlePositions = CalculateParticlePositions(item, tooltipSize, time);
            Vector2[] lensPositions = CalculateLensPositions(item, tooltipSize, time);
            Vector2[] sourcePositions = new Vector2[LensSourceCount];
            float[] sourceStrengths = new float[LensSourceCount];

            for (int i = 0; i < LensSourceCount; i++)
            {
                Vector2 localPosition = lensPositions[i] + new Vector2(RenderPadding);
                sourcePositions[i] = new Vector2(localPosition.X / targetWidth, localPosition.Y / targetHeight);
                sourceStrengths[i] = lensPositions[i].X < 0f ? 0f : 1f;
            }

            DrawTooltipToTarget(item, Main.spriteBatch, lines, bounds, time, particlePositions);
            DrawTargetWithShader(Main.spriteBatch, new Vector2(bounds.X - RenderPadding, bounds.Y - RenderPadding), targetWidth, targetHeight, sourcePositions, sourceStrengths);
        }

        private static void EnsureRenderTarget(int width, int height)
        {
            GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;
            if (textTarget is not null && !textTarget.IsDisposed && textTarget.Width >= width && textTarget.Height >= height)
                return;

            textTarget?.Dispose();
            textTarget = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }

        private static void DrawTooltipToTarget(Item item, SpriteBatch sb, ReadOnlyCollection<DrawableTooltipLine> lines, Rectangle bounds, float time, Vector2[] particlePositions)
        {
            GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;

            sb.End();
            graphicsDevice.SetRenderTarget(textTarget);
            graphicsDevice.Clear(Color.Transparent);

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

            foreach (DrawableTooltipLine line in lines)
            {
                Vector2 localPosition = new Vector2(line.X - bounds.X + RenderPadding, line.Y - bounds.Y + RenderPadding);
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    DrawAccretionEdge(sb, line.Font, line.Text, localPosition, line.Rotation, line.Origin, line.BaseScale, time);
                    DrawDarkCore(sb, line.Font, line.Text, localPosition, line.Rotation, line.Origin, line.BaseScale, time);
                    continue;
                }

                Color lineColor = line.OverrideColor ?? line.Color;
                ChatManager.DrawColorCodedStringWithShadow(
                    sb,
                    line.Font,
                    line.Text,
                    localPosition,
                    lineColor,
                    line.Rotation,
                    line.Origin,
                    line.BaseScale,
                    line.MaxWidth,
                    line.Spread);
            }

            DrawFallingParticles(item, sb, particlePositions, time);

            sb.End();
            graphicsDevice.SetRenderTarget(null);
        }

        private static void DrawTargetWithShader(SpriteBatch sb, Vector2 screenPosition, int width, int height, Vector2[] sourcePositions, float[] sourceStrengths)
        {
            Effect shader = GetTextLensShader();
            if (shader is null)
            {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
                sb.Draw(textTarget, screenPosition, new Rectangle(0, 0, width, height), Color.White);
                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
                return;
            }

            shader.Parameters["sourcePositions"]?.SetValue(sourcePositions);
            shader.Parameters["sourceStrengths"]?.SetValue(sourceStrengths);
            shader.Parameters["sourceCount"]?.SetValue(LensSourceCount);
            shader.Parameters["lensRadius"]?.SetValue(TextLensRadius);
            shader.Parameters["distortionStrength"]?.SetValue(TextLensStrength);

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, shader, Main.UIScaleMatrix);
            sb.Draw(textTarget, screenPosition, new Rectangle(0, 0, width, height), Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        }

        private static Effect GetTextLensShader()
        {
            if (textLensShader is not null && !textLensShader.IsDisposed)
                return textLensShader;

            try
            {
                byte[] shaderBytes = ModContent.GetInstance<global::TestMod.TestMod>().GetFileBytes(CompiledShaderPath);
                textLensShader = new Effect(Main.graphics.GraphicsDevice, shaderBytes);
            }
            catch
            {
                textLensShader = null;
            }

            return textLensShader;
        }

        private static Rectangle CalculateTooltipBounds(ReadOnlyCollection<DrawableTooltipLine> lines)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            foreach (DrawableTooltipLine line in lines)
            {
                Vector2 size = line.Font.MeasureString(line.Text) * line.BaseScale;
                minX = MathF.Min(minX, line.X);
                minY = MathF.Min(minY, line.Y);
                maxX = MathF.Max(maxX, line.X + size.X);
                maxY = MathF.Max(maxY, line.Y + size.Y);
            }

            int x = (int)MathF.Floor(minX);
            int y = (int)MathF.Floor(minY);
            int width = Math.Max(1, (int)MathF.Ceiling(maxX - minX));
            int height = Math.Max(1, (int)MathF.Ceiling(maxY - minY));
            return new Rectangle(x, y, width, height);
        }

        private static void DrawAccretionEdge(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, float rotation, Vector2 origin, Vector2 baseScale, float time)
        {
            float pulse = 0.82f + 0.18f * MathF.Sin(time * 1.35f);
            for (int i = 0; i < EdgeGlowLayers; i++)
            {
                float progress = i / (float)EdgeGlowLayers;
                float angle = MathHelper.TwoPi * progress + time * 0.35f;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.55f) * EdgeGlowRadius;
                Color edgeColor = Color.Lerp(AccretionOrange, AccretionGold, 0.5f + 0.5f * MathF.Sin(angle + time * 1.8f));
                edgeColor.A = 0;

                ChatManager.DrawColorCodedString(
                    sb,
                    font,
                    text,
                    position + offset,
                    edgeColor * EdgeGlowOpacity * pulse,
                    rotation,
                    origin,
                    baseScale);
            }
        }

        private static void DrawDarkCore(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, float rotation, Vector2 origin, Vector2 baseScale, float time)
        {
            for (int i = 0; i < ShadowLayers; i++)
            {
                float angle = MathHelper.TwoPi * i / ShadowLayers;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * ShadowRadius;
                ChatManager.DrawColorCodedString(
                    sb,
                    font,
                    text,
                    position + offset,
                    Color.Black * 0.7f,
                    rotation,
                    origin,
                    baseScale);
            }

            float innerPulse = 0.5f + 0.5f * MathF.Sin(time * 0.9f);
            Color textColor = Color.Lerp(TextCoreColor, TextInnerColor, innerPulse * 0.28f);
            textColor.A = 255;

            ChatManager.DrawColorCodedStringShadow(
                sb,
                font,
                text,
                position,
                Color.Black * 0.85f,
                rotation,
                origin,
                baseScale);

            ChatManager.DrawColorCodedString(
                sb,
                font,
                text,
                position,
                textColor,
                rotation,
                origin,
                baseScale);
        }

        private static Vector2[] CalculateParticlePositions(Item item, Vector2 areaSize, float time)
        {
            Vector2[] positions = new Vector2[FallingParticleCount];
            int seedBase = item.type * 397 + 17;

            for (int i = 0; i < FallingParticleCount; i++)
            {
                float phaseOffset = i * (ParticleCycleSeconds / FallingParticleCount);
                float localTime = PositiveModulo(time + phaseOffset + Hash01(seedBase + i * 31), ParticleCycleSeconds);
                if (localTime > ParticleVisibleSeconds)
                {
                    positions[i] = new Vector2(-9999f);
                    continue;
                }

                float progress = localTime / ParticleVisibleSeconds;
                float xJitter = (Hash01(seedBase + i * 59) - 0.5f) * 8f;
                float sideDirection = Hash01(seedBase + i * 109) < 0.5f ? -1f : 1f;
                float sideDrift = sideDirection * ParticleSideDrift * progress;
                positions[i] = new Vector2(
                    areaSize.X * Hash01(seedBase + i * 83) + xJitter + sideDrift,
                    -8f + progress * ParticleFallDistance);
            }

            return positions;
        }

        private static Vector2[] CalculateLensPositions(Item item, Vector2 areaSize, float time)
        {
            Vector2[] positions = new Vector2[LensSourceCount];
            int seedBase = item.type * 733 + 91;

            for (int i = 0; i < LensSourceCount; i++)
            {
                float phaseOffset = i * (LensCycleSeconds / LensSourceCount);
                float localTime = PositiveModulo(time + phaseOffset + Hash01(seedBase + i * 29), LensCycleSeconds);
                if (localTime > LensActiveSeconds)
                {
                    positions[i] = new Vector2(-9999f);
                    continue;
                }

                float progress = localTime / LensActiveSeconds;
                float fadeCenter = MathF.Sin(progress * MathHelper.Pi);
                if (fadeCenter < 0.18f)
                {
                    positions[i] = new Vector2(-9999f);
                    continue;
                }

                float baseX = areaSize.X * Hash01(seedBase + i * 61);
                float baseY = areaSize.Y * Hash01(seedBase + i * 97);
                float driftAngle = MathHelper.TwoPi * Hash01(seedBase + i * 131);
                Vector2 drift = new Vector2(MathF.Cos(driftAngle), MathF.Sin(driftAngle)) * 8f * progress;
                positions[i] = new Vector2(baseX, baseY) + drift;
            }

            return positions;
        }

        private static void DrawFallingParticles(Item item, SpriteBatch sb, Vector2[] particlePositions, float time)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int seedBase = item.type * 397 + 17;

            for (int i = 0; i < FallingParticleCount; i++)
            {
                if (particlePositions[i].X < 0f)
                    continue;

                float phaseOffset = i * (ParticleCycleSeconds / FallingParticleCount);
                float localTime = PositiveModulo(time + phaseOffset + Hash01(seedBase + i * 31), ParticleCycleSeconds);
                float progress = MathHelper.Clamp(localTime / ParticleVisibleSeconds, 0f, 1f);
                float fade = MathF.Sin(progress * MathHelper.Pi) * ParticleOpacity;
                Vector2 particlePosition = particlePositions[i] + new Vector2(RenderPadding);

                DrawSoftPixel(sb, pixel, particlePosition, ParticleGlowRadius, AccretionOrange * 0.04f * fade);
                DrawSoftPixel(sb, pixel, particlePosition, ParticleRadius, AccretionGold * fade);
            }
        }

        private static void DrawSoftPixel(SpriteBatch sb, Texture2D pixel, Vector2 center, float radius, Color color)
        {
            Rectangle destination = new Rectangle(
                (int)(center.X - radius),
                (int)(center.Y - radius),
                Math.Max(1, (int)(radius * 2f)),
                Math.Max(1, (int)(radius * 2f)));

            sb.Draw(pixel, destination, color);
        }

        private static float PositiveModulo(float value, float divisor)
        {
            float result = value % divisor;
            return result < 0f ? result + divisor : result;
        }

        private static float Hash01(int value)
        {
            uint hash = (uint)value;
            hash ^= hash >> 16;
            hash *= 0x7feb352dU;
            hash ^= hash >> 15;
            hash *= 0x846ca68bU;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }
}
