using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Graphics.DynamicText;
using TestMod.Common.Graphics.DynamicText.Fonts;
using TestMod.Common.Systems;

namespace TestMod.Content.Rarities
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

        // 文字透镜：只作用于物品名，半径使用局部纹理像素，避免宽高比改变扭曲形状。
        public const int LensSourceCount = 3;
        public const float LensCycleSeconds = 7.2f;
        public const float LensActiveSeconds = 4.8f;
        public const float LensSpawnMargin = 18f;
        public const float LensWanderRadius = 10f;
        public const float TextLensRadius = 8f;
        public const float TextLensStrength = 1.1f;

        // 物品名闪光：参考 Infernum 稀有度的横向辉光和红色闪电粒子，但换成事件视界的橙金/深红风格。
        public const int NameGlowLayers = 12;
        public const float NameGlowRadius = 2.4f;
        public const int NameSparkleSpawnChance = 3;
        public const int MaxNameSparkles = 18;

        private static RenderTarget2D textTarget;
        private static Effect textLensShader;
        private static bool loggedShaderLoadFailure;
        private static bool loggedDrawFailure;
        private static readonly List<NameSparkle> nameSparkles = [];
        private static string sparkleOwnerText;
        private static SpriteBatch nameSpriteBatch;
        private static NameRenderRequest? pendingName;
        private static NameRenderRequest? preparedName;
        private static NameRenderRequest? layoutName;
        private static Rectangle layoutBounds;
        private static Rectangle preparedBounds;
        private static Vector2[] preparedSourcePositions;
        private static float[] preparedSourceStrengths;
        private static readonly Vector2[] particlePositions = new Vector2[FallingParticleCount];
        private static readonly Vector2[] lensPositions = new Vector2[LensSourceCount];
        private static readonly Vector2[] sourcePositions = new Vector2[LensSourceCount];
        private static readonly float[] sourceStrengths = new float[LensSourceCount];
        // RenderTrail 在返回前完成顶点上传和绘制，顺序调用可复用这组控制点。
        private static readonly Vector2[] trailPoints = new Vector2[5];

        // 只保存绘制参数；鼠标位置不进入缓存键，移动 tooltip 不需要重建布局。
        private readonly record struct NameRenderRequest(int ItemType, string Text, DynamicSpriteFont Font, float Rotation, Vector2 Origin, Vector2 Scale, Color? OverrideColor, float MaxWidth, float Spread);

        public override Color RarityColor => Color.Lerp(TextInnerColor, AccretionGold, 0.35f);

        public static bool TryDrawName(Item item, DrawableTooltipLine line)
        {
            if (Main.dedServ || string.IsNullOrEmpty(line.Text))
                return false;

            NameRenderRequest request = new(item.type, line.Text, line.Font, line.Rotation, line.Origin, line.BaseScale, line.OverrideColor, line.MaxWidth, line.Spread);
            pendingName = request;
            // 第一次悬停或切换物品时先画普通文字；下一帧帧前准备完成后再启用特效。
            if (preparedName != request || textTarget is null || textTarget.IsDisposed)
                return false;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            try
            {
                // 此处只合成纹理，绝不 SetRenderTarget 或 Clear，保留已经画好的世界和 UI。
                Vector2 position = new(line.X + preparedBounds.X - RenderPadding, line.Y + preparedBounds.Y - RenderPadding);
                DrawTargetWithShader(sb, position, preparedBounds.Width + RenderPadding * 2, preparedBounds.Height + RenderPadding * 2, preparedSourcePositions, preparedSourceStrengths);
                return true;
            }
            catch (Exception exception)
            {
                LogDrawFailure(exception);
                // 特效失败时让原版继续画名字，不能留下空白 tooltip。
                return false;
            }
            finally
            {
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
            }
        }

        public static void PrepareNameTarget()
        {
            NameRenderRequest? request = pendingName;
            pendingName = null;
            preparedName = null;
            if (Main.dedServ || request is not NameRenderRequest name)
                return;

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            RenderTargetBinding[] previousTargets = device.GetRenderTargets();
            Viewport previousViewport = device.Viewport;
            Rectangle previousScissor = device.ScissorRectangle;
            BlendState previousBlend = device.BlendState;
            DepthStencilState previousDepth = device.DepthStencilState;
            RasterizerState previousRasterizer = device.RasterizerState;
            // Luminance 的 primitive 绘制会修改共享 CullNone，而不仅是替换状态引用。
            bool previousCullNoneScissor = RasterizerState.CullNone.ScissorTestEnable;
            SamplerState previousSampler = device.SamplerStates[0];
            try
            {
                // 普通名称的布局只在参数变化时重算；自定义 snippet 保留动态尺寸。
                DynamicTextLayout layout = GetNameLayout(name.Font, name.Text, name.Scale, name.MaxWidth);
                if (layoutName != name || !layout.CanCache)
                {
                    layoutBounds = CalculateNameBounds(name);
                    layoutName = name;
                }
                Rectangle bounds = layoutBounds;
                EnsureRenderTarget(bounds.Width + RenderPadding * 2, bounds.Height + RenderPadding * 2);
                if (nameSpriteBatch is null || nameSpriteBatch.IsDisposed || nameSpriteBatch.GraphicsDevice != device)
                {
                    nameSpriteBatch?.Dispose();
                    nameSpriteBatch = new SpriteBatch(device);
                }

                float time = Main.GlobalTimeWrappedHourly;
                Vector2 nameSize = bounds.Size();
                CalculateParticlePositions(name.ItemType, nameSize, time, particlePositions);
                CalculateLensPositions(name.ItemType, nameSize, time, lensPositions, sourceStrengths);
                for (int i = 0; i < LensSourceCount; i++)
                {
                    Vector2 localPosition = lensPositions[i] + new Vector2(RenderPadding);
                    sourcePositions[i] = new Vector2(localPosition.X / textTarget.Width, localPosition.Y / textTarget.Height);
                }

                // 使用独立 SpriteBatch，不干预 Main.spriteBatch 的 Begin/End 生命周期。
                DrawNameToTarget(name, nameSpriteBatch, bounds, time, particlePositions);
                preparedBounds = bounds;
                preparedSourcePositions = sourcePositions;
                preparedSourceStrengths = sourceStrengths;
                preparedName = name;
            }
            catch (Exception exception)
            {
                LogDrawFailure(exception);
            }
            finally
            {
                device.SetRenderTargets(previousTargets);
                device.Viewport = previousViewport;
                device.ScissorRectangle = previousScissor;
                device.BlendState = previousBlend;
                device.DepthStencilState = previousDepth;
                RasterizerState.CullNone.ScissorTestEnable = previousCullNoneScissor;
                device.RasterizerState = previousRasterizer;
                device.SamplerStates[0] = previousSampler;
            }
        }

        private static void LogDrawFailure(Exception exception)
        {
            if (loggedDrawFailure)
                return;

            loggedDrawFailure = true;
            ModContent.GetInstance<global::TestMod.TestMod>().Logger.Warn($"Failed to draw Event Horizon item name: {exception}");
        }

        private static void EnsureRenderTarget(int width, int height)
        {
            GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;
            if (textTarget is not null && !textTarget.IsDisposed && textTarget.Width >= width && textTarget.Height >= height)
                return;

            textTarget?.Dispose();
            textTarget = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }

        private static void DrawNameToTarget(NameRenderRequest name, SpriteBatch sb, Rectangle bounds, float time, Vector2[] particlePositions)
        {
            GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;

            graphicsDevice.SetRenderTarget(textTarget);
            graphicsDevice.Clear(Color.Transparent);

            BeginTooltipTargetBatch(sb);

            try
            {
                Vector2 localPosition = new(-bounds.X + RenderPadding, -bounds.Y + RenderPadding);
                DrawRegistryNameEffects(name.ItemType, sb, name.Font, name.Text, localPosition, name.Rotation, name.Origin, name.Scale, time, name.OverrideColor, name.MaxWidth, name.Spread);
                Vector2 textSize = GetNameLayout(name.Font, name.Text, name.Scale, name.MaxWidth).Size * name.Scale;
                Vector2 sparkleCenter = localPosition + (textSize * new Vector2(0.5f, 0.45f) - name.Origin * name.Scale).RotatedBy(name.Rotation);
                DrawNameSparkles(sb, name.Text, sparkleCenter, textSize, time);
                DrawFallingParticles(name.ItemType, sb, particlePositions, time);
            }
            finally
            {
                sb.End();
            }
        }

        private static void DrawTargetWithShader(SpriteBatch sb, Vector2 screenPosition, int width, int height, Vector2[] sourcePositions, float[] sourceStrengths)
        {
            Effect shader = GetTextLensShader();
            if (shader is not null)
            {
                shader.Parameters["sourcePositions"]?.SetValue(sourcePositions);
                shader.Parameters["sourceStrengths"]?.SetValue(sourceStrengths);
                shader.Parameters["sourceCount"]?.SetValue(LensSourceCount);
                shader.Parameters["textureSize"]?.SetValue(new Vector2(textTarget.Width, textTarget.Height));
                shader.Parameters["lensRadius"]?.SetValue(TextLensRadius);
                shader.Parameters["distortionStrength"]?.SetValue(TextLensStrength);
            }

            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, shader, Main.UIScaleMatrix);
            try
            {
                sb.Draw(textTarget, screenPosition, new Rectangle(0, 0, width, height), Color.White);
            }
            finally
            {
                sb.End();
            }
        }

        private static Effect GetTextLensShader()
        {
            if (textLensShader is not null && !textLensShader.IsDisposed)
                return textLensShader;
            if (loggedShaderLoadFailure)
                return null;

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
            SpriteBatch oldBatch = nameSpriteBatch;

            textTarget = null;
            textLensShader = null;
            nameSpriteBatch = null;
            pendingName = null;
            preparedName = null;
            layoutName = null;
            layoutBounds = default;
            preparedBounds = default;
            preparedSourcePositions = null;
            preparedSourceStrengths = null;
            loggedShaderLoadFailure = false;
            loggedDrawFailure = false;
            nameSparkles.Clear();
            sparkleOwnerText = null;

            if (Main.dedServ || oldTarget is null && oldShader is null && oldBatch is null)
                return;

            Main.QueueMainThreadAction(() =>
            {
                oldTarget?.Dispose();
                oldShader?.Dispose();
                oldBatch?.Dispose();
            });
        }

        private static void BeginTooltipTargetBatch(SpriteBatch sb)
        {
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        }

        private static void DrawRegistryNameEffects(int itemType, SpriteBatch sb, DynamicSpriteFont font, string text, Vector2 position, float rotation, Vector2 origin, Vector2 baseScale, float time, Color? overrideColor, float maxWidth, float spread)
        {
            Vector2 textSize = GetNameLayout(font, text, baseScale, maxWidth).Size * baseScale;
            Vector2 glowCenter = position + (new Vector2(textSize.X * 0.5f, textSize.Y * 0.42f) - origin * baseScale).RotatedBy(rotation);
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
                itemType * 991 + text.GetHashCode(),
                overrideColor,
                maxWidth,
                spread);
        }

        private static DynamicTextLayout GetNameLayout(DynamicSpriteFont font, string text, Vector2 scale, float maxWidth) =>
            DynamicTextLayout.Get(new DynamicTextFont(font), text, scale.X > 0f && maxWidth > 0f ? maxWidth / scale.X : -1f);

        private static Rectangle CalculateNameBounds(NameRenderRequest name)
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;

            Vector2 size = GetNameLayout(name.Font, name.Text, name.Scale, name.MaxWidth).Size * name.Scale;
            Vector2 origin = name.Origin * name.Scale;
            Vector2[] corners = [Vector2.Zero, new Vector2(size.X, 0f), size, new Vector2(0f, size.Y)];
            foreach (Vector2 corner in corners)
            {
                Vector2 position = (corner - origin).RotatedBy(name.Rotation);
                minX = MathF.Min(minX, position.X);
                minY = MathF.Min(minY, position.Y);
                maxX = MathF.Max(maxX, position.X);
                maxY = MathF.Max(maxY, position.Y);
            }

            int x = (int)MathF.Floor(minX);
            int y = (int)MathF.Floor(minY);
            int width = Math.Max(1, (int)MathF.Ceiling(maxX - minX));
            int height = Math.Max(1, (int)MathF.Ceiling(maxY - minY));
            return new Rectangle(x, y, width, height);
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
            try
            {
                Vector2 start = glowCenter + new Vector2(textSize.X * -0.62f, 0f);
                Vector2 end = glowCenter + new Vector2(textSize.X * 0.62f, 0f);
                Vector2[] points = trailPoints;
                FillWavyLinePoints(points, start, end, 1.2f, time * 1.7f);
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
            }
            finally
            {
                BeginTooltipTargetBatch(sb);
            }
        }

        private static void CalculateParticlePositions(int itemType, Vector2 areaSize, float time, Vector2[] positions)
        {
            int seedBase = itemType * 397 + 17;

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

        }

        private static void CalculateLensPositions(int itemType, Vector2 areaSize, float time, Vector2[] positions, float[] strengths)
        {
            int seedBase = itemType * 733 + 91;

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

        private static void DrawFallingParticles(int itemType, SpriteBatch sb, Vector2[] particlePositions, float time)
        {
            if (textTarget is null || textTarget.IsDisposed)
                return;

            int seedBase = itemType * 397 + 17;
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
            try
            {
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
            }
            finally
            {
                BeginTooltipTargetBatch(sb);
            }
        }

        private static void DrawNameSparkles(SpriteBatch sb, string text, Vector2 center, Vector2 textSize, float time)
        {
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

            if (nameSparkles.Count <= 0 || textTarget is null || textTarget.IsDisposed)
                return;

            DrawNameSparkleTextures(sb, center);

            sb.End();
            try
            {
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
            }
            finally
            {
                BeginTooltipTargetBatch(sb);
            }
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
            Vector2[] points = trailPoints;
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

        private static void FillWavyLinePoints(Vector2[] points, Vector2 start, Vector2 end, float waveAmplitude, float phase)
        {
            Vector2 delta = end - start;
            Vector2 normal = new Vector2(-delta.Y, delta.X).SafeNormalize(Vector2.Zero);
            for (int i = 0; i < points.Length; i++)
            {
                float completion = i / (float)(points.Length - 1);
                float wave = MathF.Sin(completion * MathHelper.Pi + phase) * waveAmplitude;
                points[i] = Vector2.Lerp(start, end, completion) + normal * wave;
            }
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
