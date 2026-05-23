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

        // CoreTextureRadiusScale：BlackHoleCore 贴图采样半径。
        // 只影响贴图本身的缩放，不影响程序化黑圆；贴图环和黑圆对不上时优先调这里。
        public const float CoreTextureRadiusScale = 1.64f;

        // ProceduralCore*：shader 额外画出的黑圆遮罩。
        // 黑圆比 BlackHoleCore 小：调大 Inner/Outer；黑圆太大：调小 Inner/Outer。
        // Opacity 越高，贴图透明中心越容易被纯黑遮住。
        public const float ProceduralCoreInnerRadiusFactor = 0.38f;
        public const float ProceduralCoreOuterRadiusFactor = 0.92f;
        public const float ProceduralCoreOpacity = 0.94f;

        // AccretionOuterRadiusFactor：吸积盘外缘范围，相对于注册进来的黑洞半径。
        // 数值越大，吸积盘越宽，也越容易把外部画面视觉上连接到中心。
        public const float AccretionOuterRadiusFactor = 3f;

        // AccretionInnerStartFactor / AccretionInnerFullFactor：吸积盘从黑核外侧开始出现的位置。
        // 数值越低，吸积盘越贴近黑核，可以减少核心和吸积盘之间的透明隔离带。
        public const float AccretionInnerStartFactor = 0.54f;
        public const float AccretionInnerFullFactor = 0.66f;

        // AccretionNoiseStrength：流动噪声对吸积盘亮度的贡献。
        public const float AccretionNoiseStrength = 2.55f;

        // 吸积盘贴图只允许在接近水平的范围内缓慢摆动，避免整圈 360 度旋转。
        // MaxTiltDegrees 应保持小于 30；BaseTiltDegrees 可以给整体一个固定倾角。
        public const float AccretionDiskBaseTiltDegrees = -8f;
        public const float AccretionDiskMaxTiltDegrees = 22f;
        public const float AccretionDiskTiltSpeed = 0.32f;

        // 盘内材质流动参数。整体倾角仍只小幅摆动，下面这些只让盘面纹理沿切向流动。
        public const float AccretionDiskFlowStrength = 0.034f;
        public const float AccretionDiskFlowSpeed = 0.72f;
        public const float AccretionDiskFlowScale = 2.4f;
        public const float AccretionDiskFlowHighlight = 0.36f;

        // HotRingRadiusFactor / HotRingWidthFactor：事件视界亮环的位置和厚度，相对于黑核半径。
        // 亮环越宽，越能遮住黑核边缘附近的透镜断层感。
        public const float HotRingRadiusFactor = 1.02f;
        public const float HotRingWidthFactor = 0.48f;

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
        private static readonly float[] sourceDiskModes = new float[MaxLensSources];
        private static Texture2D requestedOcclusionTexture;
        private static Texture2D requestedDiskTexture;
        private static Texture2D requestedDiskFlowTexture;
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
            => RegisterSource(worldCenter, radius, strength, occlusionRadius, occlusionOpacity, 0f, Vector3.Zero, occlusionTexture, BlackHoleVisualStyle.Default);

        public static void RegisterBlackHole(
            Vector2 worldCenter,
            float radius,
            float opacity,
            Color accretionDiskColor,
            Texture2D coreTexture = null)
        {
            BlackHoleVisualStyle style = coreTexture is null
                ? BlackHoleVisualStyle.Default
                : new BlackHoleVisualStyle(BlackHoleCoreMode.Texture, BlackHoleDiskMode.Default, coreTexture);

            RegisterBlackHole(worldCenter, radius, opacity, accretionDiskColor, style);
        }

        public static void RegisterBlackHole(
            Vector2 worldCenter,
            float radius,
            float opacity,
            Color accretionDiskColor,
            BlackHoleVisualStyle visualStyle)
            => RegisterSource(worldCenter, radius, opacity, radius * CoreRadiusFactor, opacity, opacity, accretionDiskColor.ToVector3(), null, visualStyle);

        private static void RegisterSource(
            Vector2 worldCenter,
            float radius,
            float strength,
            float occlusionRadius,
            float occlusionOpacity,
            float accretionOpacity,
            Vector3 accretionColor,
            Texture2D occlusionTexture,
            BlackHoleVisualStyle visualStyle)
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
            Texture2D coreTexture = visualStyle.GetRequestedCoreTexture() ?? occlusionTexture;
            Texture2D diskTexture = visualStyle.GetRequestedDiskTexture();
            Texture2D diskFlowTexture = visualStyle.GetRequestedDiskFlowTexture();
            bool useMaterialDisk = visualStyle.DiskMode == BlackHoleDiskMode.Material
                && (diskTexture is not null && diskFlowTexture is not null || BlackHoleVisualAssetSystem.HasDiskMaterial);

            sourceDiskModes[requestCount] = useMaterialDisk ? 1f : 0f;
            requestedOcclusionTexture ??= coreTexture;
            requestedDiskTexture ??= diskTexture;
            requestedDiskFlowTexture ??= diskFlowTexture;
            requestCount++;
        }

        private static Vector2 WorldToZoomedScreenPosition(Vector2 worldPosition, Vector2 screenSize, Vector2 zoom)
        {
            Vector2 unzoomedScreenPosition = worldPosition - Main.screenPosition;
            Vector2 screenCenter = screenSize * 0.5f;
            return (unzoomedScreenPosition - screenCenter) * zoom + screenCenter;
        }

        public static float GetAccretionDiskTiltRadians(float time)
        {
            float baseTilt = MathHelper.ToRadians(AccretionDiskBaseTiltDegrees);
            float maxTilt = MathHelper.ToRadians(MathHelper.Clamp(AccretionDiskMaxTiltDegrees, 0f, 29.9f));
            return baseTilt + MathF.Sin(time * AccretionDiskTiltSpeed) * maxTilt;
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
            float[] packedDiskModes = new float[MaxLensSources];
            Array.Copy(sourcePositions, packedPositions, MaxLensSources);
            Array.Copy(sourceRadii, packedRadii, MaxLensSources);
            Array.Copy(sourceStrengths, packedStrengths, MaxLensSources);
            Array.Copy(sourceOcclusionRadii, packedOcclusionRadii, MaxLensSources);
            Array.Copy(sourceOcclusionOpacities, packedOcclusionOpacities, MaxLensSources);
            Array.Copy(sourceAccretionOpacities, packedAccretionOpacities, MaxLensSources);
            Array.Copy(sourceAccretionColors, packedAccretionColors, MaxLensSources);
            Array.Copy(sourceDiskModes, packedDiskModes, MaxLensSources);

            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            filter.TrySetParameter("sourcePositions", packedPositions);
            filter.TrySetParameter("sourceRadii", packedRadii);
            filter.TrySetParameter("sourceStrengths", packedStrengths);
            filter.TrySetParameter("sourceOcclusionRadii", packedOcclusionRadii);
            filter.TrySetParameter("sourceOcclusionOpacities", packedOcclusionOpacities);
            filter.TrySetParameter("sourceAccretionOpacities", packedAccretionOpacities);
            filter.TrySetParameter("sourceAccretionColors", packedAccretionColors);
            filter.TrySetParameter("sourceDiskModes", packedDiskModes);
            filter.TrySetParameter("sourceCount", requestCount);
            filter.TrySetParameter("time", Main.GlobalTimeWrappedHourly);
            filter.TrySetParameter("distortionStrength", 1f);
            filter.TrySetParameter("maxLensingAngle", LensingAngle);
            filter.TrySetParameter("lensingFalloff", LensingFalloff);
            filter.TrySetParameter("accretionOuterRadiusFactor", AccretionOuterRadiusFactor);
            filter.TrySetParameter("accretionInnerStartFactor", AccretionInnerStartFactor);
            filter.TrySetParameter("accretionInnerFullFactor", AccretionInnerFullFactor);
            filter.TrySetParameter("accretionNoiseStrength", AccretionNoiseStrength);
            filter.TrySetParameter("coreTextureRadiusScale", CoreTextureRadiusScale);
            filter.TrySetParameter("proceduralCoreInnerRadiusFactor", ProceduralCoreInnerRadiusFactor);
            filter.TrySetParameter("proceduralCoreOuterRadiusFactor", ProceduralCoreOuterRadiusFactor);
            filter.TrySetParameter("proceduralCoreOpacity", ProceduralCoreOpacity);
            filter.TrySetParameter("diskBaseTiltRadians", MathHelper.ToRadians(AccretionDiskBaseTiltDegrees));
            filter.TrySetParameter("diskMaxTiltRadians", MathHelper.ToRadians(MathHelper.Clamp(AccretionDiskMaxTiltDegrees, 0f, 29.9f)));
            filter.TrySetParameter("diskTiltSpeed", AccretionDiskTiltSpeed);
            filter.TrySetParameter("diskFlowStrength", AccretionDiskFlowStrength);
            filter.TrySetParameter("diskFlowSpeed", AccretionDiskFlowSpeed);
            filter.TrySetParameter("diskFlowScale", AccretionDiskFlowScale);
            filter.TrySetParameter("diskFlowHighlight", AccretionDiskFlowHighlight);
            filter.TrySetParameter("hotRingRadiusFactor", HotRingRadiusFactor);
            filter.TrySetParameter("hotRingWidthFactor", HotRingWidthFactor);
            filter.TrySetParameter("aspectRatioCorrectionFactor", new Vector2(screenSize.X / screenSize.Y, 1f));
            filter.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
            filter.SetTexture(requestedOcclusionTexture ?? BlackHoleVisualAssetSystem.CoreTexture, 1, SamplerState.LinearClamp);
            filter.SetTexture(AntaresVisualAssetSystem.HaloNoise, 2, SamplerState.LinearWrap);
            filter.SetTexture(requestedDiskTexture ?? BlackHoleVisualAssetSystem.DiskTexture, 3, SamplerState.LinearClamp);
            filter.SetTexture(requestedDiskFlowTexture ?? BlackHoleVisualAssetSystem.DiskFlowTexture, 4, SamplerState.LinearWrap);
            filter.Activate();
        }

        private static void ClearRequests()
        {
            requestCount = 0;
            requestedOcclusionTexture = null;
            requestedDiskTexture = null;
            requestedDiskFlowTexture = null;
            for (int i = 0; i < MaxLensSources; i++)
            {
                sourcePositions[i] = Vector2.One * -9999f;
                sourceRadii[i] = 0.0001f;
                sourceStrengths[i] = 0f;
                sourceOcclusionRadii[i] = 0f;
                sourceOcclusionOpacities[i] = 0f;
                sourceAccretionOpacities[i] = 0f;
                sourceAccretionColors[i] = Vector3.Zero;
                sourceDiskModes[i] = 0f;
            }
        }
    }
}
