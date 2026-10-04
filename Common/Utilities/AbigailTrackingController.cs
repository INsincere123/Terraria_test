using System;
using Microsoft.Xna.Framework;

namespace TestMod.Common.Utilities
{
    /// <summary>阿比盖尔的运动与攻击状态；速度统一以游戏帧计算，输出换算为每次 AI 的位移。</summary>
    internal static class AbigailTrackingController
    {
        internal const float TargetRange = 3200f;
        internal const float ReturnRange = 5600f;
        internal const float AttackEnterDistance = 20f;
        internal const float AttackExitDistance = 96f;
        private const float Acceleration = 3f;

        internal static float ChaseSpeed(int extraSlots) => Math.Min(36f, 18f + 1.4f * Math.Max(0, extraSlots));
        internal static float AttackDuration(int extraSlots) => Math.Max(5f, 40f - Math.Max(0, extraSlots) * 4f);

        internal static float BoundaryDistanceSquared(Vector2 center, Rectangle bounds)
        {
            Vector2 nearest = new(MathHelper.Clamp(center.X, bounds.Left, bounds.Right),
                MathHelper.Clamp(center.Y, bounds.Top, bounds.Bottom));
            return Vector2.DistanceSquared(center, nearest);
        }

        // ai[0]: 0=追赶，1=返回，>=2=原版攻击周期。仅 owner 决定阶段，远端推进已有周期。
        internal static float UpdateState(float state, float boundaryDistanceSq, bool hasTarget,
            float duration, float frameStep, bool authority, out bool phaseChanged, out bool attackStarted)
        {
            int oldPhase = Phase(state);
            attackStarted = false;
            if (authority)
            {
                if (!hasTarget) state = 1f;
                else if (state >= 2f && boundaryDistanceSq > AttackExitDistance * AttackExitDistance) state = 0f;
                else if (state < 2f)
                {
                    state = 0f;
                    if (boundaryDistanceSq <= AttackEnterDistance * AttackEnterDistance)
                    {
                        state = 2f;
                        attackStarted = true;
                    }
                }
            }

            if (state >= 2f && !attackStarted)
            {
                state += frameStep;
                // 原版整数计时在 >duration 时重启，即周期包含 duration+1 这个边界。
                // 使用固定游戏帧边界并保留余量，避免多次更新缩短每个攻击周期。
                if (state >= duration + 1f)
                {
                    state = 2f + (state - duration - 1f);
                    attackStarted = true;
                }
            }
            phaseChanged = Phase(state) != oldPhase;
            return state;
        }

        private static int Phase(float state) => state >= 2f ? 2 : state == 1f ? 1 : 0;

        internal static Vector2 Move(Vector2 center, Vector2 velocity, Vector2 targetCenter,
            Vector2 targetVelocity, float maxSpeed, float frameStep, bool attacking, bool returning)
        {
            Vector2 offset = targetCenter - center;
            Vector2 desired;
            if (attacking || returning)
            {
                // 攻击跟随目标当前中心；返回同样使用位置反馈，接近停靠点时自然刹车。
                desired = targetVelocity + offset * 0.25f;
            }
            else
            {
                float prediction = Math.Min(12f, offset.Length() / maxSpeed) * 0.5f;
                Vector2 predictedOffset = offset + targetVelocity * prediction;
                float lengthSq = predictedOffset.LengthSquared();
                desired = lengthSq > 0f ? predictedOffset * (maxSpeed / MathF.Sqrt(lengthSq)) : Vector2.Zero;
            }

            desired = Limit(desired, maxSpeed);
            Vector2 frameVelocity = velocity / frameStep;
            frameVelocity += Limit(desired - frameVelocity, Acceleration * frameStep);
            return frameVelocity * frameStep;
        }

        private static Vector2 Limit(Vector2 vector, float limit)
        {
            float lengthSq = vector.LengthSquared();
            return lengthSq > limit * limit ? vector * (limit / MathF.Sqrt(lengthSq)) : vector;
        }
    }
}
