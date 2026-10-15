using System;
using System.Collections.Generic;

namespace TestMod.Common.Mechanics.Lifetaking
{
    // 记录的是伤害机会而非命中次数。被无敌/免疫拒绝的单发机会也不能晚些补发。
    internal sealed class LifetakingLedger
    {
        private readonly Dictionary<(int Slot, uint Generation, int Phase), ulong> attempts = new();
        internal bool HasAttempts => attempts.Count != 0;

        internal bool Reserve(int slot, uint generation, int phase, ulong tick, int interval)
        {
            var key = (slot, generation, phase);
            if (generation == 0 || attempts.TryGetValue(key, out ulong next) && tick < next) return false;
            attempts[key] = interval <= 0 ? ulong.MaxValue : tick + (ulong)Math.Max(1, interval);
            return true;
        }

        internal bool Contains(int slot, uint generation, int phase)
            => attempts.ContainsKey((slot, generation, phase));

        internal bool Blocks(int slot, uint generation, int phase, ulong tick)
            => attempts.TryGetValue((slot, generation, phase), out ulong next) && tick < next;

        internal void Clear() => attempts.Clear();

        internal LifetakingLedger Clone()
        {
            var clone = new LifetakingLedger();
            foreach (var pair in attempts) clone.attempts.Add(pair.Key, pair.Value);
            return clone;
        }
    }
}
