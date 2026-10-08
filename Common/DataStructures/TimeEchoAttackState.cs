using System;

namespace TestMod.Common.DataStructures
{
    // 等待包含持续期和随后冷却；中断只关闭效果，不能缩短再次开启的等待。
    internal sealed class TimeEchoAttackState
    {
        internal const int Duration = 300; // 持续30秒，方便连续测试
        internal const int Cooldown = 900;   // 冷却1秒，也可以设为0
        internal const int TotalWait = Duration + Cooldown;
        internal int ActiveTicks { get; private set; }
        internal int WaitTicks { get; private set; }
        internal uint Generation { get; private set; }

        internal bool TryStart(bool owned, bool alive, bool phantom)
        {
            if (!owned || !alive || !phantom || WaitTicks != 0) return false;
            ActiveTicks = Duration;
            WaitTicks = TotalWait;
            Generation++;
            return true;
        }

        internal void Tick(bool owned, bool alive)
        {
            if (!owned) { Clear(); return; }
            if (WaitTicks > 0) WaitTicks--;
            if (!alive) ActiveTicks = 0;
            else if (ActiveTicks > 0) ActiveTicks--;
        }

        internal void Stop() => ActiveTicks = 0;
        internal void Restore(int wait) { ActiveTicks = 0; WaitTicks = Math.Clamp(wait, 0, TotalWait); }
        internal void Synchronize(int active, int wait, uint generation)
        {
            WaitTicks = Math.Clamp(wait, 0, TotalWait);
            ActiveTicks = Math.Clamp(active, 0, Math.Min(Duration, Math.Max(0, WaitTicks - Cooldown)));
            Generation = generation;
        }
        internal void Clear() { ActiveTicks = WaitTicks = 0; }
    }
}
