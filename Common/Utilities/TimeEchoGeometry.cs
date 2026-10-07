using System;
using Microsoft.Xna.Framework;

namespace TestMod.Common.Utilities
{
    internal static class TimeEchoGeometry
    {
        internal static void GetDrawBounds(Matrix transform, int width, int height, Vector2 screenPosition,
            out Vector2 minimum, out Vector2 maximum)
        {
            Matrix inverse = Matrix.Invert(transform);
            Vector2 a = Vector2.Transform(Vector2.Zero, inverse);
            Vector2 b = Vector2.Transform(new Vector2(width, 0f), inverse);
            Vector2 c = Vector2.Transform(new Vector2(0f, height), inverse);
            Vector2 d = Vector2.Transform(new Vector2(width, height), inverse);
            minimum = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d)) + screenPosition - new Vector2(800f);
            maximum = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d)) + screenPosition + new Vector2(800f);
        }

        // Liang–Barsky：固定四次测试，跨地图的线不会按距离分段或生成粒子。
        internal static bool ClipLine(ref Vector2 start, ref Vector2 end, Vector2 minimum, Vector2 maximum)
        {
            Vector2 delta = end - start;
            float first = 0f, last = 1f;
            if (!Clip(-delta.X, start.X - minimum.X, ref first, ref last) ||
                !Clip(delta.X, maximum.X - start.X, ref first, ref last) ||
                !Clip(-delta.Y, start.Y - minimum.Y, ref first, ref last) ||
                !Clip(delta.Y, maximum.Y - start.Y, ref first, ref last)) return false;
            end = start + delta * last;
            start += delta * first;
            return true;
        }

        private static bool Clip(float denominator, float numerator, ref float first, ref float last)
        {
            if (MathF.Abs(denominator) < 0.00001f) return numerator >= 0f;
            float ratio = numerator / denominator;
            if (denominator < 0f)
            {
                if (ratio > last) return false;
                first = Math.Max(first, ratio);
            }
            else
            {
                if (ratio < first) return false;
                last = Math.Min(last, ratio);
            }
            return true;
        }
    }
}
