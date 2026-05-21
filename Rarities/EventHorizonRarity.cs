using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using TestMod.Common.DynamicText;
using TestMod.Common.Systems;

namespace TestMod.Rarities
{
    public class EventHorizonRarity : ModRarity
    {
        public const string CompiledShaderPath = "Assets/Effects/EventHorizonRarityShader.fxc";

        // ==================== 事件视界稀有度参数 ====================
        // 主体颜色：保持深紫黑的黑洞核心感，但略抬亮度，避免默认 tooltip 背景上糊成一团。
        public static readonly Color TextCoreColor = new Color(46, 18, 66);
        public static readonly Color TextInnerColor = new Color(118, 52, 158);

        // 吸积盘边缘：橙金光只做轻微描边，数值过高会影响阅读。
        public static readonly Color AccretionGold = new Color(255, 166, 55);
        public static readonly Color AccretionOrange = new Color(255, 95, 34);
        public const float EdgeGlowRadius = 1.8f;
        public const int EdgeGlowLayers = 8;
        public const float EdgeGlowOpacity = 0.42f;

        // 事件视界阴影：贴近文字的一层暗描边，让主体不会被金边吞掉。
        public const float ShadowRadius = 1.45f;
        public const int ShadowLayers = 8;

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
        public const float LensCycleSeconds = 7.2f;
        public const float LensActiveSeconds = 4.8f;
        public const float LensSpawnMargin = 18f;
        public const float LensWanderRadius = 10f;
        public const float TextLensRadius = 0.026f;
        public const float TextLensStrength = 3.6f;

        // 物品名闪光：参考 Infernum 稀有度的横向辉光和红色闪电粒子，但换成事件视界的橙金/深红风格。
        public const int NameGlowLayers = 12;
        public const float NameGlowRadius = 2.4f;
        public const int NameSparkleSpawnChance = 3;
        public const int MaxNameSparkles = 18;

        private static RenderTarget2D textTarget;
        private static Effect textLensShader;
        private static bool loggedShaderLoadFailure;
        private static readonly List<NameSparkle> nameSparkles = [];
        private static string sparkleOwnerText;

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
            Vector2[] lensPositions = CalculateLensPositions(item, tooltipSize, time, out float[] sourceStrengths);
            Vector2[] sourcePositions = new Vector2[LensSourceCount];

            for (int i = 0; i < LensSourceCount; i++)
            {
                Vector2 localPosition = lensPositions[i] + new Vector2(RenderPadding);
                sourcePositions[i] = new Vector2(localPosition.X / targetWidth, localPosition.Y / targetHeight);
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

            BeginTooltipTargetBatch(sb);

            foreach (DrawableTooltipLine line in lines)
            {
                Vector2 localPosition = new Vector2(line.X - bounds.X + RenderPadding, line.Y - bounds.Y + RenderPadding);
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    DrawRegistryNameEffects(item, sb, line.Font, line.Text, localPosition, line.Rotation, line.Origin, line.BaseScale, time);
                    DrawNameSparkles(sb, line.Font, line.Text, localPosition, line.BaseScale, time);
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
            catch (Exception exception)
            {
                if (!loggedShaderLoadFailure)
                {
                    ModContent.GetInstance<global::TestMod.TestMod>().Logger.Warn($"Failed to load Event Horizon rarity shader: {exception.Message}");
                    loggedShaderLoadFailure = true;
                }

                textLensShader = null;
            }

            return textLensShader;
        }

        public static void UnloadResources()
        {
            RenderTarget2D oldTarget = textTarget;
            Effect oldShader = textLensShader;

            textTarget = null;
            textLensShader = null;
            loggedShaderLoadFailure = false;
            nameSparkles.Clear();
            sparkleOwnerText = null;

            if (Main.dedServ || oldTarget is null && oldShader is null)
                return;

            Main.QueueMainThreadAction(() =>
            {
                oldTarget?.Dispose();
                oldShader?.Dispose();
            });
        }

        private static void BeginTooltipTargetBatch(SpriteBatch sb)
        {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        }

        private static void DrawRegistryNameEffects(Item item, SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, float rotation, Vector2 origin, Vector2 baseScale, float time)
        {
            Vector2 textSize = font.MeasureString(text) * baseScale;
            Vector2 glowCenter = position + new Vector2(textSize.X * 0.5f, textSize.Y * 0.42f);
            float pulse = 0.78f + 0.22f * MathF.Sin(time * 2.4f);

            DrawNameTexturedAura(sb, glowCenter, textSize, pulse, time);
            DrawNameGlowPrimitive(sb, glowCenter, textSize, pulse, time);

            DynamicTextTooltipRenderer.DrawText(
                sb,
                font,
                text,
                position,
                rotation,
                origin,
                baseScale,
                DynamicTextStyleRegistry.Get(DynamicTextStyleRegistry.RarityEventHorizon),
                item.type * 991 + text.GetHashCode());
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

        private static void DrawNameGlow(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, float rotation, Vector2 origin, Vector2 baseScale, float time)
        {
            Vector2 textSize = font.MeasureString(text) * baseScale;
            Vector2 glowCenter = position + new Vector2(textSize.X * 0.5f, textSize.Y * 0.42f);
            float pulse = 0.78f + 0.22f * MathF.Sin(time * 2.4f);
            DrawNameTexturedAura(sb, glowCenter, textSize, pulse, time);
            DrawNameGlowPrimitive(sb, glowCenter, textSize, pulse, time);

            for (int i = 0; i < NameGlowLayers; i++)
            {
                float angle = MathHelper.TwoPi * i / NameGlowLayers;
                Vector2 offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.55f) * NameGlowRadius * pulse;
                Color glowColor = Color.Lerp(new Color(115, 18, 28), AccretionGold, 0.42f + 0.18f * MathF.Sin(time * 1.7f + angle));
                glowColor.A = 0;

                ChatManager.DrawColorCodedString(
                    sb,
                    font,
                    text,
                    position + offset,
                    glowColor * 0.28f,
                    rotation,
                    origin,
                    baseScale);
            }
        }

        private static void DrawNameTexturedAura(SpriteBatch sb, Vector2 glowCenter, Vector2 textSize, float pulse, float time)
        {
            if (!AntaresVisualAssetSystem.HasSoftStarMote)
                return;

            Texture2D texture = AntaresVisualAssetSystem.SoftStarMote;
            Vector2 textureOrigin = texture.Size() * 0.5f;
            Vector2 baseScale = new(textSize.X / texture.Width * 1.18f, textSize.Y / texture.Height * 0.58f);
            Color warmHalo = new Color(255, 142, 42, 0) * (0.34f * pulse);
            Color deepHalo = new Color(104, 30, 128, 0) * (0.22f * pulse);

            sb.Draw(texture, glowCenter + new Vector2(0f, textSize.Y * 0.02f), null, deepHalo, 0f, textureOrigin, baseScale * new Vector2(1.16f, 0.72f), SpriteEffects.None, 0f);
            sb.Draw(texture, glowCenter, null, warmHalo, MathF.Sin(time * 0.5f) * 0.08f, textureOrigin, baseScale, SpriteEffects.None, 0f);
            sb.Draw(texture, glowCenter + new Vector2(textSize.X * 0.02f, -textSize.Y * 0.03f), null, AccretionGold * (0.12f * pulse), -MathF.Sin(time * 0.42f) * 0.1f, textureOrigin, baseScale * new Vector2(0.72f, 0.28f), SpriteEffects.None, 0f);
        }

        private static void DrawNameGlowPrimitive(SpriteBatch sb, Vector2 glowCenter, Vector2 textSize, float pulse, float time)
        {
            if (textTarget is null || textTarget.IsDisposed)
                return;

            sb.End();

            Vector2 start = glowCenter + new Vector2(textSize.X * -0.62f, 0f);
            Vector2 end = glowCenter + new Vector2(textSize.X * 0.62f, 0f);
            Vector2[] points = CreateWavyLinePoints(start, end, 5, 1.2f, time * 1.7f);
            AddScreenPosition(points);

            Color glowColor = AccretionOrange * (0.2f * pulse);
            glowColor.A = 0;
            PrimitiveRenderer.RenderTrail(
                points,
                new PrimitiveSettings(
                    completion => (8f + 2f * MathF.Sin(completion * MathHelper.Pi)) * pulse,
                    completion => glowColor * MathF.Sin(completion * MathHelper.Pi),
                    Smoothen: true,
                    ProjectionAreaWidth: textTarget.Width,
                    ProjectionAreaHeight: textTarget.Height,
                    UseUnscaledMatrix: true),
                16);

            BeginTooltipTargetBatch(sb);
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
                    Color.Black * 0.82f,
                    rotation,
                    origin,
                    baseScale);
            }

            float innerPulse = 0.5f + 0.5f * MathF.Sin(time * 0.9f);
            Color textColor = Color.Lerp(TextCoreColor, TextInnerColor, 0.16f + innerPulse * 0.36f);
            textColor.A = 255;

            ChatManager.DrawColorCodedStringShadow(
                sb,
                font,
                text,
                position,
                Color.Black * 0.95f,
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

        private static Vector2[] CalculateLensPositions(Item item, Vector2 areaSize, float time, out float[] strengths)
        {
            Vector2[] positions = new Vector2[LensSourceCount];
            strengths = new float[LensSourceCount];
            int seedBase = item.type * 733 + 91;

            for (int i = 0; i < LensSourceCount; i++)
            {
                int sourceSeed = seedBase + i * 173;
                float phaseOffset = i * (LensCycleSeconds / LensSourceCount);
                float localTime = PositiveModulo(time + phaseOffset + Hash01(sourceSeed + 29) * LensCycleSeconds, LensCycleSeconds);
                if (localTime > LensActiveSeconds)
                {
                    positions[i] = new Vector2(-9999f);
                    strengths[i] = 0f;
                    continue;
                }

                float progress = localTime / LensActiveSeconds;
                float fade = SmoothStep(0f, 0.18f, progress) * (1f - SmoothStep(0.78f, 1f, progress));
                float easedProgress = SmoothStep(0f, 1f, progress);

                Vector2 start = RandomLensPoint(areaSize, sourceSeed + 61);
                Vector2 control = RandomLensPoint(areaSize, sourceSeed + 97);
                Vector2 end = RandomLensPoint(areaSize, sourceSeed + 131);

                float inverseProgress = 1f - easedProgress;
                Vector2 pathPosition =
                    start * inverseProgress * inverseProgress +
                    control * 2f * inverseProgress * easedProgress +
                    end * easedProgress * easedProgress;

                float wobbleAngle = MathHelper.TwoPi * Hash01(sourceSeed + 197) + time * (0.55f + Hash01(sourceSeed + 211) * 0.6f);
                Vector2 wobble = new Vector2(MathF.Cos(wobbleAngle), MathF.Sin(wobbleAngle * 0.83f)) * LensWanderRadius * fade;

                positions[i] = pathPosition + wobble;
                strengths[i] = fade * MathHelper.Lerp(0.55f, 1.15f, Hash01(sourceSeed + 251));
            }

            return positions;
        }

        private static Vector2 RandomLensPoint(Vector2 areaSize, int seed)
        {
            float x = MathHelper.Lerp(-LensSpawnMargin, areaSize.X + LensSpawnMargin, Hash01(seed));
            float y = MathHelper.Lerp(-LensSpawnMargin, areaSize.Y + LensSpawnMargin, Hash01(seed + 37));
            return new Vector2(x, y);
        }

        private static float SmoothStep(float edge0, float edge1, float value)
        {
            if (edge0 == edge1)
                return value >= edge1 ? 1f : 0f;

            float progress = MathHelper.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
            return progress * progress * (3f - 2f * progress);
        }

        private static void DrawFallingParticles(Item item, SpriteBatch sb, Vector2[] particlePositions, float time)
        {
            if (textTarget is null || textTarget.IsDisposed)
                return;

            int seedBase = item.type * 397 + 17;
            bool hasParticles = false;
            for (int i = 0; i < FallingParticleCount; i++)
            {
                if (particlePositions[i].X >= 0f)
                {
                    hasParticles = true;
                    break;
                }
            }

            if (!hasParticles)
                return;

            sb.End();

            for (int i = 0; i < FallingParticleCount; i++)
            {
                if (particlePositions[i].X < 0f)
                    continue;

                float phaseOffset = i * (ParticleCycleSeconds / FallingParticleCount);
                float localTime = PositiveModulo(time + phaseOffset + Hash01(seedBase + i * 31), ParticleCycleSeconds);
                float progress = MathHelper.Clamp(localTime / ParticleVisibleSeconds, 0f, 1f);
                float fade = MathF.Sin(progress * MathHelper.Pi) * ParticleOpacity;
                Vector2 particlePosition = particlePositions[i] + new Vector2(RenderPadding);

                DrawPrimitiveCircle(particlePosition, ParticleGlowRadius, AccretionOrange * 0.07f * fade, 14);
                DrawPrimitiveCircle(particlePosition, ParticleRadius, AccretionGold * fade, 10);
            }

            BeginTooltipTargetBatch(sb);
        }

        private static void DrawNameSparkles(SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, Vector2 baseScale, float time)
        {
            Vector2 textSize = font.MeasureString(text) * baseScale;
            if (sparkleOwnerText != text)
            {
                nameSparkles.Clear();
                sparkleOwnerText = text;
            }

            if (nameSparkles.Count < MaxNameSparkles && Main.rand.NextBool(NameSparkleSpawnChance))
            {
                Vector2 spawnArea = new(textSize.X, textSize.Y * 0.45f);
                Vector2 spawnPosition = new(
                    Main.rand.NextFloat(-spawnArea.X * 0.5f, spawnArea.X * 0.5f),
                    Main.rand.NextFloat(-spawnArea.Y * 0.65f, spawnArea.Y * 0.35f));

                Vector2 velocity = spawnPosition.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.16f) * Main.rand.NextFloat(0.18f, 0.48f);
                velocity.Y -= 0.16f;

                nameSparkles.Add(new NameSparkle(
                    Main.rand.Next(42, 64),
                    Main.rand.NextFloat(0.7f, 1.18f),
                    velocity.ToRotation() + MathHelper.PiOver2,
                    spawnPosition,
                    velocity,
                    Color.Lerp(new Color(190, 38, 42), AccretionOrange, Main.rand.NextFloat(0.82f))));
            }

            Vector2 center = position + new Vector2(textSize.X * 0.5f, textSize.Y * 0.45f);
            if (nameSparkles.Count <= 0 || textTarget is null || textTarget.IsDisposed)
                return;

            DrawNameSparkleTextures(sb, center);

            sb.End();

            for (int i = nameSparkles.Count - 1; i >= 0; i--)
            {
                NameSparkle sparkle = nameSparkles[i];
                sparkle.Update();
                if (sparkle.Time >= sparkle.Lifetime)
                {
                    nameSparkles.RemoveAt(i);
                    continue;
                }

                sparkle.Draw(center);
                nameSparkles[i] = sparkle;
            }

            BeginTooltipTargetBatch(sb);
        }

        private static void DrawNameSparkleTextures(SpriteBatch sb, Vector2 center)
        {
            if (!AntaresVisualAssetSystem.HasSoftStarMote)
                return;

            Texture2D texture = AntaresVisualAssetSystem.SoftStarMote;
            Vector2 origin = texture.Size() * 0.5f;
            foreach (NameSparkle sparkle in nameSparkles)
            {
                if (sparkle.Scale <= 0f)
                    continue;

                Vector2 drawPosition = center + sparkle.Position;
                float textureScale = sparkle.Scale * 0.055f;
                Color color = Color.Lerp(sparkle.Color, AccretionGold, 0.35f) * (0.42f * sparkle.Scale);
                color.A = 0;

                sb.Draw(texture, drawPosition, null, color, sparkle.Rotation, origin, textureScale, SpriteEffects.None, 0f);
                sb.Draw(texture, drawPosition, null, Color.White * (0.12f * sparkle.Scale), -sparkle.Rotation * 0.7f, origin, textureScale * 0.36f, SpriteEffects.None, 0f);
            }
        }

        private struct NameSparkle
        {
            public int Time;
            public int Lifetime;
            public float MaxScale;
            public float Scale;
            public float Rotation;
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;

            public NameSparkle(int lifetime, float scale, float rotation, Vector2 position, Vector2 velocity, Color color)
            {
                Time = 0;
                Lifetime = lifetime;
                MaxScale = scale;
                Scale = 0f;
                Rotation = rotation;
                Position = position;
                Velocity = velocity;
                Color = color;
            }

            public void Update()
            {
                Position += Velocity;
                Velocity *= 0.972f;
                Rotation += 0.026f;
                Time++;

                int timeLeft = Lifetime - Time;
                float grow = MathHelper.Clamp(Time / 10f, 0f, 1f);
                float shrink = MathHelper.Clamp(timeLeft / 24f, 0f, 1f);
                float flicker = 0.82f + 0.18f * MathF.Sin(Time * 0.73f + MaxScale * 6.1f);
                Scale = MaxScale * MathF.Min(grow, shrink) * flicker;
            }

            public void Draw(Vector2 center)
            {
                if (Scale <= 0f)
                    return;

                Color drawColor = Color * (0.72f * Scale);
                drawColor.A = 0;

                Vector2 drawPosition = center + Position;
                DrawLightningPrimitive(drawPosition, Rotation, 14f * Scale, 0.9f * Scale, drawColor);
                DrawLightningPrimitive(drawPosition, Rotation + MathHelper.PiOver2, 6f * Scale, 0.55f * Scale, drawColor * 0.65f);
                DrawLightningPrimitive(drawPosition, Rotation - 0.72f, 8f * Scale, 0.42f * Scale, AccretionGold * (0.42f * Scale));
            }
        }

        private static void DrawLightningPrimitive(Vector2 center, float rotation, float length, float thickness, Color color)
        {
            if (textTarget is null || textTarget.IsDisposed)
                return;

            Vector2 direction = rotation.ToRotationVector2();
            Vector2 normal = new(-direction.Y, direction.X);
            Vector2[] points = new Vector2[5];
            for (int i = 0; i < points.Length; i++)
            {
                float completion = i / (float)(points.Length - 1);
                float zigzag = (i % 2 == 0 ? -1f : 1f) * thickness * 1.15f;
                points[i] = center + direction * ((completion - 0.5f) * length) + normal * zigzag;
            }

            AddScreenPosition(points);

            PrimitiveRenderer.RenderTrail(
                points,
                new PrimitiveSettings(
                    completion => thickness * (0.35f + MathF.Sin(completion * MathHelper.Pi) * 0.9f),
                    completion => color * MathHelper.Clamp(MathF.Sin(completion * MathHelper.Pi) * 1.25f, 0f, 1f),
                    Smoothen: true,
                    ProjectionAreaWidth: textTarget.Width,
                    ProjectionAreaHeight: textTarget.Height,
                    UseUnscaledMatrix: true),
                8);
        }

        private static void DrawPrimitiveCircle(Vector2 center, float radius, Color color, int sideCount)
        {
            if (textTarget is null || textTarget.IsDisposed || radius <= 0f)
                return;

            Color drawColor = color;
            drawColor.A = 0;
            PrimitiveRenderer.RenderCircle(
                center + Main.screenPosition,
                new PrimitiveSettingsCircle(
                    _ => radius,
                    _ => drawColor,
                    ProjectionAreaWidth: textTarget.Width,
                    ProjectionAreaHeight: textTarget.Height,
                    UseUnscaledMatrix: true),
                sideCount);
        }

        private static Vector2[] CreateWavyLinePoints(Vector2 start, Vector2 end, int pointCount, float waveAmplitude, float phase)
        {
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

        private static void AddScreenPosition(Vector2[] points)
        {
            for (int i = 0; i < points.Length; i++)
                points[i] += Main.screenPosition;
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
