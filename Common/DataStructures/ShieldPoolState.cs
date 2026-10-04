using System;

namespace TestMod.Common.DataStructures
{
    /// <summary>
    /// 护盾数值状态。与绘制、网络及 Terraria 钩子分离，所有战斗运算使用未取整的余额。
    /// </summary>
    internal sealed class ShieldPoolState
    {
        internal const int RechargeDelayFrames = 11 * 60;
        internal const int RechargeDurationFrames = 6 * 60;

        private double normal;
        private double temporary;
        internal float NormalShield => (float)normal;
        internal float NormalMax { get; private set; }
        internal float TemporaryShield => (float)temporary;
        internal float TemporaryInitial { get; private set; }
        internal int TemporaryFrames { get; private set; }
        internal int TemporaryDuration { get; private set; }
        internal int HitTimer { get; private set; } = RechargeDelayFrames;
        internal bool Initialized { get; private set; }
        internal bool IsFragile { get; private set; }
        internal bool IsRecovering { get; private set; }
        internal float Current => NormalMax > 0f ? (float)(normal + temporary) : 0f;
        internal float Maximum => NormalMax > 0f ? NormalMax + TemporaryMaximum : 0f;
        internal float TemporaryMaximum => TemporaryDuration > 0
            ? TemporaryInitial * (TemporaryFrames / (float)TemporaryDuration) : 0f;

        internal void SetEquipment(float maximum, bool allowTemporary)
        {
            NormalMax = float.IsFinite(maximum) ? Math.Max(0f, maximum) : 0f;
            if (NormalMax > 0f)
            {
                if (!Initialized)
                {
                    Initialized = true;
                    normal = NormalMax;
                }
                normal = Math.Min(normal, NormalMax);
                if (normal >= NormalMax)
                {
                    IsFragile = false;
                    IsRecovering = false;
                }
            }
            else
                IsRecovering = false; // 卸装保留余额和脆弱状态，不进行恢复。

            if (!allowTemporary || NormalMax <= 0f)
                ClearTemporary();
        }

        internal void AddTemporary(float amount, int duration)
        {
            if (NormalMax <= 0f || !float.IsFinite(amount) || amount <= 0f || duration <= 0)
                return;
            temporary = TemporaryInitial = amount;
            TemporaryFrames = TemporaryDuration = duration;
        }

        private void ClearTemporary()
        {
            temporary = 0d;
            TemporaryInitial = 0f;
            TemporaryFrames = TemporaryDuration = 0;
        }

        internal (bool Broke, bool RecoveryStarted) Tick(float decayPerSecond)
        {
            bool hadShield = Current > 0f;
            if (TemporaryFrames > 0)
            {
                TemporaryFrames--;
                temporary = Math.Max(0d, temporary - TemporaryInitial / (double)TemporaryDuration);
                if (TemporaryFrames == 0 || temporary <= 0d)
                    ClearTemporary();
            }
            if (NormalMax > 0f && decayPerSecond > 0f)
                normal = Math.Max(0d, normal - decayPerSecond / 60d);

            bool broke = hadShield && Current <= 0f;
            bool started = false;
            // 先完整等待 660 帧，下一帧才执行第一次恢复。
            if (HitTimer < RechargeDelayFrames)
            {
                HitTimer++;
                IsRecovering = false;
            }
            else if (NormalMax > 0f && normal < NormalMax)
            {
                started = !IsRecovering;
                IsRecovering = true;
                IsFragile = true;
                normal = Math.Min(NormalMax, normal + NormalMax / (double)RechargeDurationFrames);
                // 双精度累加的舍入误差不能让第 360 帧仍处于脆弱状态。
                if (normal >= NormalMax * (1d - 1e-12d))
                {
                    normal = NormalMax;
                    IsFragile = false;
                    IsRecovering = false;
                }
            }
            else
                IsRecovering = false;

            return (broke, started);
        }

        internal (float Absorbed, int LifeDamage, bool Broke) TakeHit(int damage)
        {
            if (damage <= 0)
                return (0f, damage, false);

            HitTimer = 0;
            IsRecovering = false; // 脆弱状态不会被受击清除。
            if (NormalMax <= 0f)
                return (0f, damage, false); // 卸装受击仍重置等待，但不能消耗保留的余额。
            double before = normal + temporary;
            double fromTemporary = Math.Min(damage, temporary);
            temporary -= fromTemporary;
            double fromNormal = Math.Min(damage - fromTemporary, normal);
            normal -= fromNormal;
            double absorbed = fromTemporary + fromNormal;
            // 渐进恢复的双精度余数不能留下幽灵护盾或制造 1 点虚假穿透。
            double epsilon = Math.Max(1d, Math.Max(NormalMax, TemporaryInitial)) * 1e-12d;
            if (normal <= epsilon) normal = 0d;
            if (temporary <= epsilon)
                ClearTemporary();
            double overflow = Math.Max(0d, damage - absorbed);
            if (overflow <= epsilon) overflow = 0d;
            int lifeDamage = before <= 0d || IsFragile
                ? (int)Math.Ceiling(overflow) : 0;
            return ((float)absorbed, lifeDamage, before > 0d && normal + temporary <= 0d);
        }

        internal void Die()
        {
            Initialized = true; // 复活不能再次领取首次装备满盾。
            normal = 0d;
            NormalMax = 0f;
            ClearTemporary();
            HitTimer = RechargeDelayFrames; // 复活立即开始恢复，不等待 11 秒。
            IsFragile = true;
            IsRecovering = false;
        }

        internal void Restore(float normalAmount, float maximum, float temporaryAmount, float temporaryInitial,
            int temporaryFrames, int temporaryDuration, int hitTimer, bool initialized, bool fragile, bool recovering)
        {
            normal = normalAmount;
            NormalMax = maximum;
            temporary = temporaryAmount;
            TemporaryInitial = temporaryInitial;
            TemporaryFrames = temporaryFrames;
            TemporaryDuration = temporaryDuration;
            HitTimer = hitTimer;
            Initialized = initialized;
            IsFragile = fragile;
            IsRecovering = recovering;
        }
    }
}
