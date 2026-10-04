using System;
using System.IO;
using Microsoft.Xna.Framework;

namespace TestMod.Common.Utilities
{
    /// <summary>每把剑独立的惯性穿梭运动；所有速度、转向和阻尼均按游戏帧换算。</summary>
    internal struct TerraprismaFlightController
    {
        internal const float TargetRange = 3000f;
        internal const float ReturnRange = 5600f;
        // Knightmare 每帧 6 次更新：(v + 1.4) * 0.96，稳定速度为 33.6 * 6。
        internal const float MaxSpeed = 55f;
        private const float ReferenceUpdates = 6f;
        private const float InertiaRadius = 55f;
        private const float ReturnSpeed = 36f;
        internal const int HitCooldownFrames = 15;
        internal bool Initialized { get; private set; }
        internal float Heading { get; private set; }
        internal float TurnStrength { get; private set; }

        internal void Reset() => this = default;

        internal void Start(Vector2 velocity, Vector2 offset)
        {
            Vector2 direction = velocity.LengthSquared() > 0.0001f ? velocity : offset;
            Heading = direction.LengthSquared() > 0.0001f ? MathF.Atan2(direction.Y, direction.X) : MathHelper.PiOver2;
            TurnStrength = 0f;
            Initialized = true;
        }

        internal Vector2 Update(Vector2 center, Vector2 velocity, Vector2 target, float frameStep,
            Vector2 separation)
        {
            Vector2 offset = target - center;
            if (!Initialized) Start(velocity, offset);
            float distance = offset.Length();
            if (distance > InertiaRadius)
            {
                TurnStrength = Math.Min(1f, TurnStrength + 0.3f * frameStep);
                float turn = 1f - MathF.Pow(1f - TurnStrength, ReferenceUpdates * frameStep);
                float targetAngle = MathF.Atan2(offset.Y, offset.X);
                Heading = MathHelper.WrapAngle(Heading + MathHelper.WrapAngle(targetAngle - Heading) * turn);
            }
            else TurnStrength = 0f;

            // 按参考的 6 次更新换算阻尼/加速，不让实际 MaxUpdates 改变游戏帧速度。
            float damping = MathF.Pow(0.96f, ReferenceUpdates * frameStep);
            Vector2 direction = new(MathF.Cos(Heading), MathF.Sin(Heading));
            Vector2 frameVelocity = velocity / frameStep * damping
                + direction * MaxSpeed * (1f - damping) + separation * frameStep;
            return Limit(frameVelocity, MaxSpeed) * frameStep;
        }

        internal static Vector2 Return(Vector2 center, Vector2 velocity, Vector2 idlePosition,
            Vector2 playerVelocity, float frameStep)
        {
            Vector2 desired = Limit(playerVelocity + (idlePosition - center) * 0.25f, ReturnSpeed);
            Vector2 frameVelocity = velocity / frameStep;
            frameVelocity += Limit(desired - frameVelocity, 3f * frameStep);
            return Limit(frameVelocity, ReturnSpeed) * frameStep;
        }

        internal static float Rotate(float rotation, float desired, float frameStep)
            => MathHelper.WrapAngle(rotation + MathHelper.WrapAngle(desired - rotation)
                * (1f - MathF.Pow(0.94f, frameStep)));

        private static Vector2 Limit(Vector2 value, float maximum)
        {
            float lengthSq = value.LengthSquared();
            return lengthSq > maximum * maximum ? value * (maximum / MathF.Sqrt(lengthSq)) : value;
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write(Heading);
            writer.Write(TurnStrength);
        }

        internal void Read(BinaryReader reader, bool attacking)
        {
            float heading = reader.ReadSingle();
            float strength = reader.ReadSingle();
            Initialized = attacking && float.IsFinite(heading) && float.IsFinite(strength);
            Heading = Initialized ? MathHelper.WrapAngle(heading) : 0f;
            TurnStrength = Initialized ? MathHelper.Clamp(strength, 0f, 1f) : 0f;
        }
    }
}
