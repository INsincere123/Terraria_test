using System;

namespace TestMod.Common.DataStructures
{
    internal static class TimeEchoRules
    {
        internal static int RestoreResource(int current, int historical, int maximum)
            => historical > current ? Math.Max(current, Math.Min(historical, maximum)) : current;
    }

    /// <summary>纯 tick 状态机。首样本记为 t=0，301 个样本才能读到准确的 t-300。</summary>
    internal sealed class TimeEchoTimeline<T> where T : struct
    {
        internal const int HistoryTicks = 300;
        internal const int RewindTicks = 1800;
        internal const int SwapTicks = 600;
        private readonly T[] history = new T[HistoryTicks + 1];
        private int next;
        private int count;

        internal bool Owned { get; private set; }
        internal int RewindCooldown { get; private set; }
        internal int SwapCooldown { get; private set; }
        internal bool Recording => count > 0;
        internal bool HasPhantom => Owned && count == history.Length && RewindCooldown == 0;
        // 每轮记录的序号，即使准备期之间跨过了网络采样，也只提示一次。
        internal uint RecordingGeneration { get; private set; }

        internal void Restore(bool owned, int rewind, int swap)
        {
            Owned = owned;
            RewindCooldown = owned ? Math.Clamp(rewind, 0, RewindTicks) : 0;
            SwapCooldown = owned ? Math.Clamp(swap, 0, SwapTicks) : 0;
            ClearHistory();
        }

        internal void ClearHistory()
        {
            next = count = 0;
            Array.Clear(history);
        }

        internal void Acquire(T sample)
        {
            Restore(true, 0, 0);
            BeginRecording(sample);
        }

        internal void Tick(bool alive, T sample)
        {
            if (!Owned) return;
            if (RewindCooldown > 0) RewindCooldown--;
            if (SwapCooldown > 0) SwapCooldown--;
            if (!alive)
            {
                if (Recording) ClearHistory();
                return;
            }
            // 回溯前25秒、换位前5秒均不记录；各自在冷却最后5秒建立新历史。
            if (RewindCooldown > HistoryTicks || SwapCooldown > HistoryTicks) return;
            if (!Recording) BeginRecording(sample);
            else Append(sample);
        }

        private void BeginRecording(T sample)
        {
            RecordingGeneration++;
            Append(sample);
        }

        private void Append(T sample)
        {
            history[next] = sample;
            next = (next + 1) % history.Length;
            count = Math.Min(count + 1, history.Length);
        }

        internal bool TryGetTarget(bool rewind, out T target)
        {
            target = default;
            if (!HasPhantom || (rewind ? RewindCooldown : SwapCooldown) != 0) return false;
            target = history[next];
            return true;
        }

        internal T Phantom => HasPhantom ? history[next] : default;

        // 调用者先取得目标、完成位移；两种技能均先等待，同 tick 不再调用 Tick。
        internal void Consume(bool rewind)
        {
            ClearHistory();
            if (rewind) RewindCooldown = RewindTicks;
            else SwapCooldown = SwapTicks;
        }
    }
}
