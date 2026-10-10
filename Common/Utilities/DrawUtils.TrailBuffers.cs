using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;

namespace TestMod.Common.Utilities
{
    public static partial class DrawUtils
    {
        private const int MaxPooledTrailBuffers = 4;
        private static readonly Stack<TrailDrawBuffer> TrailBuffers = new(MaxPooledTrailBuffers);

        // RenderTrail 同步消费顶点和回调。每次调用独占缓冲，避免配置着色器时的嵌套调用覆盖外层数据。
        private sealed class TrailDrawBuffer
        {
            internal readonly List<Vector2> Points = new(16);
            internal readonly List<Vector2> Smoothed = new(80);
            internal readonly TrailDrawStyle Style = new();

            internal void Clear()
            {
                Points.Clear();
                Smoothed.Clear();
                Style.Clear();
            }
        }

        // Luminance 会保留最近一次 PrimitiveSettings；回调仅持有样式，不能因此保留顶点缓冲。
        private sealed class TrailDrawStyle
        {
            internal GuidedProjectileTrailLayer Layer;
            internal float VisualTime;
            internal float Width;
            internal Color Color;
            internal bool Guided;
            private readonly PrimitiveSettings smoothSettings;
            private readonly PrimitiveSettings linearSettings;
            private PrimitiveSettings shaderSettings;

            internal TrailDrawStyle()
            {
                smoothSettings = new PrimitiveSettings(GetWidth, GetColor);
                linearSettings = new PrimitiveSettings(smoothSettings.WidthFunction, smoothSettings.ColorFunction, Smoothen: false);
            }

            internal PrimitiveSettings Settings(bool smoothen = true, ManagedShader shader = null)
            {
                if (shader == null)
                    return smoothen ? smoothSettings : linearSettings;
                if (shaderSettings == null || !ReferenceEquals(shaderSettings.Shader, shader))
                    shaderSettings = new PrimitiveSettings(smoothSettings.WidthFunction, smoothSettings.ColorFunction, Shader: shader);
                return shaderSettings;
            }

            private float GetWidth(float completion)
                => Guided ? GuidedProjectileWidth(completion, Layer.Width, Layer.WidthExponent) : Width;

            private Color GetColor(float completion)
                => Guided ? Layer.ColorFunction(completion, VisualTime + Layer.TimeOffset, Layer.Opacity)
                    : Color * MathHelper.Clamp(MathF.Sin(completion * MathHelper.Pi) * 1.18f, 0f, 1f);

            internal void Clear()
            {
                Layer = default;
                VisualTime = Width = 0f;
                Color = default;
                Guided = false;
            }
        }

        private static TrailDrawBuffer RentTrailBuffer()
            => TrailBuffers.Count > 0 ? TrailBuffers.Pop() : new TrailDrawBuffer();

        private static void ReturnTrailBuffer(TrailDrawBuffer buffer)
        {
            buffer.Clear();
            // 非常规大轨迹不常驻池中；正常 Antares 使用 13 个控制点、73 个平滑点。
            if (TrailBuffers.Count < MaxPooledTrailBuffers && buffer.Points.Capacity <= 256 && buffer.Smoothed.Capacity <= 2048)
                TrailBuffers.Push(buffer);
        }

        internal static void ClearTrailBuffers() => TrailBuffers.Clear();
    }
}
