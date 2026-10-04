using System;

namespace TestMod.Common.UI.ResourceOverlays
{
    internal enum ShieldBarStage { Ready, Waiting, FragileWaiting, Recovering, Broken }

    internal readonly record struct ShieldBarSample(bool Visible, float Normal, float Temporary,
        float Maximum, float NormalMaximum, int WaitFrames, bool Recovering, bool Fragile, float HitFlash)
    {
        internal float Total => Normal + Temporary;
        internal int WaitTenths => (Math.Max(0, WaitFrames) + 5) / 6;
        internal ShieldBarStage Stage => Normal >= NormalMaximum ? ShieldBarStage.Ready :
            Recovering ? ShieldBarStage.Recovering : Total <= 0f ? ShieldBarStage.Broken :
            Fragile ? ShieldBarStage.FragileWaiting : ShieldBarStage.Waiting;
    }

    /// <summary>两种资源条共用的客户端动画。衰减和上限变化不会伪装成受击。</summary>
    internal sealed class ShieldBarVisualState
    {
        internal const float HitDuration = 0.15f;
        internal const float TrailDuration = 0.35f;
        internal ShieldBarSample Sample { get; private set; }
        internal float NormalDisplay { get; private set; }
        internal float TemporaryDisplay { get; private set; }
        internal float TrailAmount { get; private set; }
        internal float TrailOpacity => trailRemaining / TrailDuration;
        internal float HitStrength => hitRemaining / HitDuration;
        internal float RecoveryGlow => Sample.Recovering ? 0.18f + 0.08f * (float)Math.Sin(pulse) : 0f;
        private float hitRemaining;
        private float trailRemaining;
        private float pulse;

        internal void Reset()
        {
            Sample = default;
            NormalDisplay = TemporaryDisplay = TrailAmount = 0f;
            hitRemaining = trailRemaining = pulse = 0f;
        }

        internal void Update(ShieldBarSample next, float elapsed)
        {
            if (!next.Visible || next.Maximum <= 0f)
            {
                Reset();
                return;
            }
            elapsed = Math.Clamp(elapsed, 0f, 0.1f);
            hitRemaining = Math.Max(0f, hitRemaining - elapsed);
            trailRemaining = Math.Max(0f, trailRemaining - elapsed);
            if (hitRemaining < 0.00001f) hitRemaining = 0f;
            if (trailRemaining < 0.00001f) trailRemaining = 0f;
            pulse = (pulse + elapsed * 2.7f) % (float)(Math.PI * 2d);
            bool newHit = Sample.Visible && next.HitFlash > 0f &&
                (next.HitFlash > Sample.HitFlash || next.WaitFrames > Sample.WaitFrames);
            if (!Sample.Visible)
            {
                NormalDisplay = next.Normal;
                TemporaryDisplay = next.Temporary;
            }
            else if (newHit)
            {
                hitRemaining = HitDuration;
                if (next.Total < Sample.Total)
                {
                    TrailAmount = Math.Max(trailRemaining > 0f ? TrailAmount : 0f, Sample.Total);
                    trailRemaining = TrailDuration;
                }
                // 实心部分立刻减量；滞后只出现在独立的损失残影中。
                NormalDisplay = next.Normal;
                TemporaryDisplay = next.Temporary;
            }
            else
            {
                float blend = 1f - (float)Math.Exp(-18f * elapsed);
                NormalDisplay += (next.Normal - NormalDisplay) * blend;
                TemporaryDisplay += (next.Temporary - TemporaryDisplay) * blend;
                NormalDisplay = Math.Clamp(NormalDisplay, 0f, next.NormalMaximum);
                TemporaryDisplay = Math.Clamp(TemporaryDisplay, 0f, Math.Max(0f, next.Maximum - next.NormalMaximum));
            }
            if (trailRemaining <= 0f) TrailAmount = 0f;
            Sample = next;
        }
    }
}
