using System;
using Microsoft.Xna.Framework;
using TestMod.Common.DataStructures;

namespace TestMod.Common.Utilities
{
    internal static class TimeEchoAttackGeometry
    {
        internal static void AimShot(Vector2 origin, Vector2 sourceCenter, Vector2 playerCenter,
            Vector2 sourceAim, Vector2 mouse, Vector2 velocity, out Vector2 position, out Vector2 aimedVelocity,
            TimeEchoShotCoordinates coordinates = TimeEchoShotCoordinates.MuzzleRelative)
        {
            // 天降等世界目标攻击保留原出生点与轨迹；不旋转成幻影附近的枪口攻击。
            if (coordinates == TimeEchoShotCoordinates.WorldTarget)
            { position = sourceCenter; aimedVelocity = velocity; return; }
            Vector2 aim = NormalizeOr(mouse - origin, sourceAim);
            float rotation = Angle(aim) - Angle(sourceAim);
            position = origin + Rotate(sourceCenter - playerCenter, rotation);
            aim = NormalizeOr(mouse - position, aim);
            aimedVelocity = Rotate(velocity, Angle(aim) - Angle(sourceAim));
        }
        private static Vector2 NormalizeOr(Vector2 value, Vector2 fallback)
            => value.LengthSquared() > 0.0001f ? Vector2.Normalize(value) : fallback;
        private static float Angle(Vector2 value) => MathF.Atan2(value.Y, value.X);
        private static Vector2 Rotate(Vector2 value, float rotation)
            => new(value.X * MathF.Cos(rotation) - value.Y * MathF.Sin(rotation),
                value.X * MathF.Sin(rotation) + value.Y * MathF.Cos(rotation));

        internal static bool IntersectsCircle(Vector2 center, float radius, Rectangle target)
        {
            if (radius <= 0) return false;
            Vector2 closest = new(Math.Clamp(center.X, target.Left, target.Right),
                Math.Clamp(center.Y, target.Top, target.Bottom));
            return Vector2.DistanceSquared(center, closest) <= radius * radius;
        }

        internal static bool Intersects(Vector2 center, Vector2 halfSize, float rotation, Rectangle target)
        {
            Vector2 x = new(MathF.Cos(rotation), MathF.Sin(rotation));
            Vector2 y = new(-x.Y, x.X);
            Vector2 targetHalf = new(target.Width * 0.5f, target.Height * 0.5f);
            Vector2 difference = new Vector2(target.Center.X, target.Center.Y) - center;
            return MathF.Abs(difference.X) <= targetHalf.X + MathF.Abs(x.X) * halfSize.X + MathF.Abs(y.X) * halfSize.Y &&
                MathF.Abs(difference.Y) <= targetHalf.Y + MathF.Abs(x.Y) * halfSize.X + MathF.Abs(y.Y) * halfSize.Y &&
                MathF.Abs(Vector2.Dot(difference, x)) <= halfSize.X + targetHalf.X * MathF.Abs(x.X) + targetHalf.Y * MathF.Abs(x.Y) &&
                MathF.Abs(Vector2.Dot(difference, y)) <= halfSize.Y + targetHalf.X * MathF.Abs(y.X) + targetHalf.Y * MathF.Abs(y.Y);
        }
    }
}
