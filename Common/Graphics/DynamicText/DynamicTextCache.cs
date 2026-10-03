using System;
using System.Collections.Generic;

namespace TestMod.Common.Graphics.DynamicText
{
    // 命中时更新顺序；容量同时受条目数和资源大小约束。
    internal sealed class DynamicTextCache<TKey, TValue>
    {
        private readonly Dictionary<TKey, LinkedListNode<Entry>> entries = [];
        private readonly LinkedList<Entry> usage = new();
        private readonly int capacity;
        private readonly long weightLimit;
        private readonly Action<TValue> evicted;
        private long weight;

        private readonly record struct Entry(TKey Key, TValue Value, long Weight);

        public DynamicTextCache(int capacity, long weightLimit, Action<TValue> evicted = null)
        {
            this.capacity = capacity;
            this.weightLimit = weightLimit;
            this.evicted = evicted;
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (Entry entry in usage)
                    yield return entry.Value;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (!entries.TryGetValue(key, out LinkedListNode<Entry> node))
            {
                value = default;
                return false;
            }
            usage.Remove(node);
            usage.AddFirst(node);
            value = node.Value.Value;
            return true;
        }

        public void Add(TKey key, TValue value, long entryWeight = 1)
        {
            if (entries.TryGetValue(key, out LinkedListNode<Entry> old))
                Remove(old);
            // 单项超过预算时仅淘汰这一项，不冲掉其他常用文字。
            if (entryWeight > weightLimit)
            {
                evicted?.Invoke(value);
                return;
            }
            LinkedListNode<Entry> node = usage.AddFirst(new Entry(key, value, Math.Max(1, entryWeight)));
            entries.Add(key, node);
            weight += node.Value.Weight;
            while (entries.Count > capacity || weight > weightLimit)
                Remove(usage.Last);
        }

        private void Remove(LinkedListNode<Entry> node)
        {
            entries.Remove(node.Value.Key);
            usage.Remove(node);
            weight -= node.Value.Weight;
            evicted?.Invoke(node.Value.Value);
        }

        // 卸载时调用方先收集资源，再清除索引，避免重复释放。
        public void Clear()
        {
            entries.Clear();
            usage.Clear();
            weight = 0;
        }
    }
}
