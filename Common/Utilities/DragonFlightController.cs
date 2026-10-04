using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.Common.Utilities
{
    internal enum DragonFlightPhase : byte { Chase, Pass, Return }

    internal readonly struct DragonFlightSettings
    {
        internal static readonly DragonFlightSettings Stardust = new(42f, 150f, 30f, 4f, 6f, 0.14f, 0.28f, 0.55f, 16f, 12f, 0.65f);
        internal static readonly DragonFlightSettings Phantom = new(22f, 46f, 30f, 1.5f, 2f, 0.12f, 0.24f, 0.40f, 14f, 8f, 0.5f);

        internal readonly float MinSpeed, MaxSpeed, DistanceDivisor, Acceleration, Braking;
        internal readonly float FarTurn, NearTurn, EnhancedTurn, TurningMinSpeed, PredictionFrames, PredictionFactor;

        internal DragonFlightSettings(float minSpeed, float maxSpeed, float distanceDivisor, float acceleration,
            float braking, float farTurn, float nearTurn, float enhancedTurn, float turningMinSpeed,
            float predictionFrames, float predictionFactor)
        {
            MinSpeed = minSpeed; MaxSpeed = maxSpeed; DistanceDivisor = distanceDivisor;
            Acceleration = acceleration; Braking = braking; FarTurn = farTurn; NearTurn = nearTurn;
            EnhancedTurn = enhancedTurn; TurningMinSpeed = turningMinSpeed;
            PredictionFrames = predictionFrames; PredictionFactor = predictionFactor;
        }
    }

    /// <summary>每龙头独立的攻击运动；owner 决定目标/阶段，其他端只预测运动。</summary>
    internal struct DragonFlightController
    {
        internal bool Enabled { get; private set; }
        internal int TargetIndex { get; private set; }
        internal int TargetType { get; private set; }
        internal DragonFlightPhase Phase { get; private set; }
        internal float PassRemaining => _passRemaining;
        internal float TurnLimit => _turnLimit;
        internal bool Enhanced => _enhanced;

        private Vector2 _passDirection, _lastCenter;
        private float _passRemaining, _passTime, _turnLimit, _nonProgressTime, _lastBoundaryDistance;
        private sbyte _turnSign;
        private bool _hasSample, _outsideLastStep, _enhanced;
        private NPC _targetEntity;

        internal bool Reset()
        {
            bool changed = Enabled;
            this = default;
            TargetIndex = -1;
            return changed;
        }

        internal bool SetTarget(int index, int type, bool replaced = false)
        {
            if (Enabled && TargetIndex == index && TargetType == type && !replaced) return false;
            Reset();
            Enabled = true;
            TargetIndex = index;
            TargetType = type;
            _targetEntity = Main.npc[index];
            return true;
        }

        internal NPC ResolveTarget(Projectile projectile, Vector2 origin, float range)
        {
            if (!Enabled || TargetIndex < 0 || TargetIndex >= Main.maxNPCs) return null;
            NPC target = Main.npc[TargetIndex];
            return object.ReferenceEquals(target, _targetEntity) && target.type == TargetType && target.CanBeChasedBy(projectile)
                && Vector2.DistanceSquared(origin, target.Center) < range * range ? target : null;
        }

        // frameStep=1/MaxUpdates；速度输入/输出为每 AI 更新位移，内部统一为每游戏帧速度。
        internal Vector2 Update(Vector2 center, Vector2 headSize, Rectangle targetBounds, Vector2 targetVelocity,
            Vector2 velocity, float chainLength, float frameStep, DragonFlightSettings settings,
            bool authority, bool coast, out bool changed)
        {
            changed = false;
            if (!Enabled) return velocity;
            frameStep = MathHelper.Clamp(frameStep, 0.001f, 1f);
            if (!Finite(velocity) || !float.IsFinite(velocity.LengthSquared())) velocity = Vector2.Zero;
            Vector2 targetCenter = new(targetBounds.Center.X, targetBounds.Center.Y);
            Vector2 offset = targetCenter - center;
            float boundaryDistance = BoundaryDistance(center, headSize, targetBounds);
            bool contact = boundaryDistance <= 0f;
            Vector2 direction = Normalize(velocity, Normalize(offset, Vector2.UnitX));
            float speed = velocity.Length() / frameStep;

            if (_hasSample && authority && Phase != DragonFlightPhase.Pass)
            {
                if (contact || boundaryDistance < _lastBoundaryDistance - 0.01f)
                {
                    _nonProgressTime = 0f;
                    if (_enhanced) { _enhanced = false; changed = true; }
                }
                else
                {
                    _nonProgressTime = Math.Min(30f, _nonProgressTime + frameStep);
                    if (!_enhanced && _nonProgressTime >= 30f - 0.0001f) { _enhanced = true; changed = true; }
                }
            }

            // 穿行余量只累计已离开目标碰撞区域后的前向位移，目标内路程不计入。
            if (Phase == DragonFlightPhase.Pass)
            {
                _passTime = Math.Min(12f, _passTime + frameStep);
                if (_hasSample && _outsideLastStep && !contact)
                    _passRemaining = Math.Max(0f, _passRemaining - Math.Max(0f, Vector2.Dot(center - _lastCenter, _passDirection)));
                // 1/3 等步长累加存在浮点舍入，不让 extraUpdates 多延迟一次穿行。
                if (authority && ((!contact && _passRemaining <= 0f) || _passTime >= 12f - 0.0001f))
                {
                    Phase = DragonFlightPhase.Return;
                    _nonProgressTime = 0f;
                    changed = true;
                }
            }
            else if (authority && Phase == DragonFlightPhase.Chase && contact)
            {
                Phase = DragonFlightPhase.Pass;
                _passDirection = direction;
                _passRemaining = MathHelper.Clamp(chainLength * 0.3f, 70f, 150f);
                _passTime = 0f;
                _enhanced = false;
                _nonProgressTime = 0f;
                changed = true;
            }

            _lastCenter = center;
            _lastBoundaryDistance = boundaryDistance;
            _hasSample = true;
            _outsideLastStep = !contact;

            // 命中停追仍沿输入速度飞行，但不停止穿行计时与路程累计。
            if (coast) return velocity;

            float nearFactor = 1f - Smooth01((boundaryDistance - 160f) / 440f);
            float desiredTurn = _enhanced ? settings.EnhancedTurn : MathHelper.Lerp(settings.FarTurn, settings.NearTurn, nearFactor);
            if (_turnLimit <= 0f) _turnLimit = desiredTurn;
            else _turnLimit = MathHelper.Lerp(_turnLimit, desiredTurn, 1f - MathF.Pow(0.8f, frameStep));

            float prediction = MathHelper.Clamp(offset.Length() / Math.Max(speed, 1f), 0f, settings.PredictionFrames)
                * settings.PredictionFactor * Smooth01(boundaryDistance / 160f);
            if (!Finite(targetVelocity)) targetVelocity = Vector2.Zero;
            Vector2 desiredDirection = Phase == DragonFlightPhase.Pass ? _passDirection
                : Normalize(offset + targetVelocity * prediction, direction);
            float currentAngle = MathF.Atan2(direction.Y, direction.X);
            float desiredAngle = MathF.Atan2(desiredDirection.Y, desiredDirection.X);
            float error = MathHelper.WrapAngle(desiredAngle - currentAngle);
            // 恰好反向时固定回转侧，防止浮点误差使龙头左右切换。
            if (Math.Abs(error) > MathHelper.Pi - 0.01f && _turnSign != 0) error = Math.Abs(error) * _turnSign;
            else if (Math.Abs(error) > 0.01f) _turnSign = (sbyte)Math.Sign(error);

            if (authority && Phase == DragonFlightPhase.Return && Math.Abs(error) < MathHelper.Pi / 6f)
            {
                Phase = DragonFlightPhase.Chase;
                changed = true;
            }

            float desiredSpeed = MathHelper.Clamp(settings.MinSpeed + offset.Length() / settings.DistanceDivisor,
                settings.MinSpeed, settings.MaxSpeed);
            if (Phase != DragonFlightPhase.Pass && Math.Abs(error) > MathHelper.Pi / 3f)
            {
                float radius = (Math.Min(targetBounds.Width, targetBounds.Height) + Math.Min(headSize.X, headSize.Y)) * 0.5f;
                desiredSpeed = Math.Max(settings.TurningMinSpeed, Math.Min(desiredSpeed, _turnLimit * radius * 0.8f));
            }

            float speedDelta = (desiredSpeed >= speed ? settings.Acceleration : settings.Braking) * frameStep;
            speed += MathHelper.Clamp(desiredSpeed - speed, -speedDelta, speedDelta);
            float angle = currentAngle + MathHelper.Clamp(error, -_turnLimit * frameStep, _turnLimit * frameStep);
            return new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (speed * frameStep);
        }

        private static float Smooth01(float value)
        {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float BoundaryDistance(Vector2 center, Vector2 headSize, Rectangle bounds)
        {
            float x = Math.Max(0f, Math.Abs(center.X - bounds.Center.X) - (bounds.Width + headSize.X) * 0.5f);
            float y = Math.Max(0f, Math.Abs(center.Y - bounds.Center.Y) - (bounds.Height + headSize.Y) * 0.5f);
            return MathF.Sqrt(x * x + y * y);
        }

        private static Vector2 Normalize(Vector2 value, Vector2 fallback)
            => Finite(value) && float.IsFinite(value.LengthSquared()) && value.LengthSquared() > 0.0001f
                ? Vector2.Normalize(value) : fallback;
        private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

        // 只有龙头写入此负载；位置/速度仍由引擎同步。接收时重新建立运动采样基点。
        internal void Write(BinaryWriter writer)
        {
            writer.Write(Enabled);
            if (!Enabled) return;
            writer.Write((short)TargetIndex);
            writer.Write(TargetType);
            writer.Write((byte)Phase);
            writer.Write(_passDirection.X); writer.Write(_passDirection.Y);
            writer.Write(_passRemaining); writer.Write(_passTime); writer.Write(_turnLimit);
            writer.Write(_turnSign); writer.Write(_nonProgressTime); writer.Write(_enhanced);
        }

        internal void Read(BinaryReader reader)
        {
            Reset();
            if (!reader.ReadBoolean()) return;
            int index = reader.ReadInt16(), type = reader.ReadInt32();
            DragonFlightPhase phase = (DragonFlightPhase)reader.ReadByte();
            Vector2 passDirection = new(reader.ReadSingle(), reader.ReadSingle());
            float remaining = reader.ReadSingle(), time = reader.ReadSingle(), turn = reader.ReadSingle();
            sbyte sign = reader.ReadSByte();
            float nonProgress = reader.ReadSingle();
            bool enhanced = reader.ReadBoolean();
            if (index < 0 || index >= Main.maxNPCs || type < 0 || phase > DragonFlightPhase.Return
                || !Finite(passDirection) || !float.IsFinite(remaining) || remaining < 0f || remaining > 150f
                || !float.IsFinite(time) || time < 0f || time > 12f
                || !float.IsFinite(turn) || turn < 0f || turn > 0.55f
                || sign < -1 || sign > 1 || !float.IsFinite(nonProgress) || nonProgress < 0f || nonProgress > 30f
                || (phase == DragonFlightPhase.Pass && Math.Abs(passDirection.LengthSquared() - 1f) > 0.01f)) return;
            NPC target = Main.npc[index];
            if (!target.active || target.type != type) return;
            Enabled = true; TargetIndex = index; TargetType = type; Phase = phase;
            _targetEntity = target;
            _passDirection = passDirection; _passRemaining = remaining; _passTime = time;
            _turnLimit = turn; _turnSign = sign; _nonProgressTime = nonProgress; _enhanced = enhanced;
        }
    }
}
