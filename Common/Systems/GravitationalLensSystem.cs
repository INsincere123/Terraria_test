using System;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// Per-frame registry for localized screen-space gravitational lensing.
    /// Call <see cref="Register"/> every frame while a lens source should be visible.
    /// </summary>
    public sealed class GravitationalLensSystem : ModSystem
    {
        public const string FilterName = "TestMod.GravitationalLensFilter";
        public const int MaxLensSources = 5;

        // 黑洞视觉调参区。
        // CoreRadiusFactor：黑色核心半径，相对于注册进来的黑洞半径。
        // 如果中心黑核太小，或者透镜看起来和核心脱节，就调大这个值。
        public const float CoreRadiusFactor = 1f;

        // AccretionOuterRadiusFactor：吸积盘外缘范围，相对于注册进来的黑洞半径。
        // 数值越大，吸积盘越宽，也越容易把外部画面视觉上连接到中心。
        public const float AccretionOuterRadiusFactor = 1.18f;

        // AccretionInnerStartFactor / AccretionInnerFullFactor：吸积盘从黑核外侧开始出现的位置。
        // 数值越低，吸积盘越贴近黑核，可以减少核心和吸积盘之间的透明隔离带。
        public const float AccretionInnerStartFactor = 0.54f;
        public const float AccretionInnerFullFactor = 0.66f;

        // AccretionNoiseStrength：流动噪声对吸积盘亮度的贡献。
        public const float AccretionNoiseStrength = 1.55f;

        // HotRingRadiusFactor / HotRingWidthFactor：事件视界亮环的位置和厚度，相对于黑核半径。
        // 亮环越宽，越能遮住黑核边缘附近的透镜断层感。
        public const float HotRingRadiusFactor = 1.02f;
        public const float HotRingWidthFactor = 0.28f;

        // LensingAngle：最大 UV 旋转角度。透镜边界太硬、像透明隔离层时可以适当调低。
        public const float LensingAngle = 16f;

        // LensingFalloff：透镜径向指数衰减。数值越低，吸入范围越外扩，也能减轻核心附近的硬边。
        public const float LensingFalloff = 1.35f;

        private static readonly Vector2[] sourcePositions = new Vector2[MaxLensSources];
        private static readonly float[] sourceRadii = new float[MaxLensSources];
        private static readonly float[] sourceStrengths = new float[MaxLensSources];
        private static readonly float[] sourceOcclusionRadii = new float[MaxLensSources];
        private static readonly float[] sourceOcclusionOpacities = new float[MaxLensSources];
        private static readonly float[] sourceAccretionOpacities = new float[MaxLensSources];
        private static readonly Vector3[] sourceAccretionColors = new Vector3[MaxLensSources];
        private static Texture2D requestedOcclusionTexture;
        private static int requestCount;

        /// <summary>
        /// Registers a world-space lens source for this frame.
        /// </summary>
        /// <param name="worldCenter">World-space center of the distortion.</param>
        /// <param name="radius">Approximate visual radius in pixels.</param>
        /// <param name="strength">0-1 distortion strength multiplier.</param>
        public static void Register(
            Vector2 worldCenter,
            float radius,
            float strength = 1f,
            float occlusionRadius = 0f,
            float occlusionOpacity = 0f,
            Texture2D occlusionTexture = null)
            => RegisterSource(worldCenter, radius, strength, occlusionRadius, occlusionOpacity, 0f, Vector3.Zero, occlusionTexture);

        public static void RegisterBlackHole(
            Vector2 worldCenter,
            float radius,
            float opacity,
            Color accretionDiskColor,
            Texture2D coreTexture = null)
            => RegisterSource(worldCenter, radius, opacity, radius * CoreRadiusFactor, opacity, opacity, accretionDiskColor.ToVector3(), coreTexture);

        private static void RegisterSource(
            Vector2 worldCenter,
            float radius,
            float strength,
            float occlusionRadius,
            float occlusionOpacity,
            float accretionOpacity,
            Vector3 accretionColor,
            Texture2D occlusionTexture)
        {
            if (Main.dedServ || requestCount >= MaxLensSources || radius <= 0f || strength <= 0f)
                return;

            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            if (screenSize.X <= 0f || screenSize.Y <= 0f)
                return;

            Vector2 zoom = Main.GameViewMatrix.Zoom;
            Vector2 screenPosition = WorldToZoomedScreenPosition(worldCenter, screenSize, zoom);
            float zoomScale = Math.Max(zoom.X, zoom.Y);
            float padding = radius * zoomScale * 3f;
            if (screenPosition.X < -padding || screenPosition.Y < -padding || screenPosition.X > screenSize.X + padding || screenPosition.Y > screenSize.Y + padding)
                return;

            sourcePositions[requestCount] = screenPosition / screenSize;
            sourceRadii[requestCount] = MathHelper.Clamp(radius * zoom.Y / screenSize.Y, 0.0001f, 1f);
            sourceStrengths[requestCount] = MathHelper.Clamp(strength, 0f, 1f);
            sourceOcclusionRadii[requestCount] = MathHelper.Clamp(occlusionRadius * zoom.Y / screenSize.Y, 0f, 1f);
            sourceOcclusionOpacities[requestCount] = MathHelper.Clamp(occlusionOpacity, 0f, 1f);
            sourceAccretionOpacities[requestCount] = MathHelper.Clamp(accretionOpacity, 0f, 1f);
            sourceAccretionColors[requestCount] = accretionColor;
            requestedOcclusionTexture ??= occlusionTexture;
            requestCount++;
        }

        private static Vector2 WorldToZoomedScreenPosition(Vector2 worldPosition, Vector2 screenSize, Vector2 zoom)
        {
            Vector2 unzoomedScreenPosition = worldPosition - Main.screenPosition;
            Vector2 screenCenter = screenSize * 0.5f;
            return (unzoomedScreenPosition - screenCenter) * zoom + screenCenter;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            UpdateFilter();
            ClearRequests();
        }

        private static void UpdateFilter()
        {
            if (!ShaderManager.TryGetFilter(FilterName, out ManagedScreenFilter filter))
                return;

            if (requestCount <= 0)
            {
                filter.Deactivate();
                return;
            }

            Vector2[] packedPositions = new Vector2[MaxLensSources];
            float[] packedRadii = new float[MaxLensSources];
            float[] packedStrengths = new float[MaxLensSources];
            float[] packedOcclusionRadii = new float[MaxLensSources];
            float[] packedOcclusionOpacities = new float[MaxLensSources];
            float[] packedAccretionOpacities = new float[MaxLensSources];
            Vector3[] packedAccretionColors = new Vector3[MaxLensSources];
            Array.Copy(sourcePositions, packedPositions, MaxLensSources);
            Array.Copy(sourceRadii, packedRadii, MaxLensSources);
            Array.Copy(sourceStrengths, packedStrengths, MaxLensSources);
            Array.Copy(sourceOcclusionRadii, packedOcclusionRadii, MaxLensSources);
            Array.Copy(sourceOcclusionOpacities, packedOcclusionOpacities, MaxLensSources);
            Array.Copy(sourceAccretionOpacities, packedAccretionOpacities, MaxLensSources);
            Array.Copy(sourceAccretionColors, packedAccretionColors, MaxLensSources);

            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            filter.TrySetParameter("sourcePositions", packedPositions);
            filter.TrySetParameter("sourceRadii", packedRadii);
            filter.TrySetParameter("sourceStrengths", packedStrengths);
            filter.TrySetParameter("sourceOcclusionRadii", packedOcclusionRadii);
            filter.TrySetParameter("sourceOcclusionOpacities", packedOcclusionOpacities);
            filter.TrySetParameter("sourceAccretionOpacities", packedAccretionOpacities);
            filter.TrySetParameter("sourceAccretionColors", packedAccretionColors);
            filter.TrySetParameter("sourceCount", requestCount);
            filter.TrySetParameter("time", Main.GlobalTimeWrappedHourly);
            filter.TrySetParameter("distortionStrength", 1f);
            filter.TrySetParameter("maxLensingAngle", LensingAngle);
            filter.TrySetParameter("lensingFalloff", LensingFalloff);
            filter.TrySetParameter("accretionOuterRadiusFactor", AccretionOuterRadiusFactor);
            filter.TrySetParameter("accretionInnerStartFactor", AccretionInnerStartFactor);
            filter.TrySetParameter("accretionInnerFullFactor", AccretionInnerFullFactor);
            filter.TrySetParameter("accretionNoiseStrength", AccretionNoiseStrength);
            filter.TrySetParameter("hotRingRadiusFactor", HotRingRadiusFactor);
            filter.TrySetParameter("hotRingWidthFactor", HotRingWidthFactor);
            filter.TrySetParameter("aspectRatioCorrectionFactor", new Vector2(screenSize.X / screenSize.Y, 1f));
            filter.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
            filter.SetTexture(requestedOcclusionTexture ?? BlackHoleVisualAssetSystem.CoreTexture, 1, SamplerState.LinearClamp);
            filter.SetTexture(AntaresVisualAssetSystem.HaloNoise, 2, SamplerState.LinearWrap);
            filter.Activate();
        }

        private static void ClearRequests()
        {
            requestCount = 0;
            requestedOcclusionTexture = null;
            for (int i = 0; i < MaxLensSources; i++)
            {
                sourcePositions[i] = Vector2.One * -9999f;
                sourceRadii[i] = 0.0001f;
                sourceStrengths[i] = 0f;
                sourceOcclusionRadii[i] = 0f;
                sourceOcclusionOpacities[i] = 0f;
                sourceAccretionOpacities[i] = 0f;
                sourceAccretionColors[i] = Vector3.Zero;
            }
        }
    }
}
