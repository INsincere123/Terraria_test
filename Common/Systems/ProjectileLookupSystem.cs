using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using ProjectileState = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 按游戏帧建立一次活动弹幕索引。缓存实体，不缓存位置、敌对标志或伤害。
    /// 遍历顺序保持 whoAmI 升序，与原来的 Main.projectile 扫描一致。
    /// </summary>
    internal static class ProjectileLookup
    {
        internal readonly struct Entry
        {
            public readonly int Slot, Identity, Owner, Type;
            public readonly ProjectileState State;

            public Entry(Projectile projectile, ProjectileState state)
            {
                Slot = projectile.whoAmI;
                Identity = projectile.identity;
                Owner = projectile.owner;
                Type = projectile.type;
                State = state;
            }

            public bool TryResolve(out Projectile projectile)
            {
                projectile = Main.projectile[Slot];
                // 单机 identity 可与槽位相同；额外核对每实体 Global 实例，识别同类型槽位复用。
                return projectile.active && projectile.identity == Identity
                    && ReferenceEquals(projectile.GetGlobalProjectile<ProjectileState>(), State);
            }
        }

        private static readonly List<Entry> _active = new();
        private static readonly Dictionary<(int Owner, int Type), List<Entry>> _owned = new();
        private static readonly Dictionary<int, List<Entry>> _byType = new();
        private static readonly Entry?[] _slots = new Entry?[Main.maxProjectiles];
        private static bool _hasFrame;
        private static ulong _frame;

        public static Query Owned(int owner, int type)
        {
            EnsureFrame();
            return new Query(GetOwnedList(owner, type), owner, type, hostileOnly: false);
        }

        public static Query OfType(int type)
        {
            EnsureFrame();
            return new Query(GetTypeList(type), -1, type, hostileOnly: false);
        }

        public static Query Hostile()
        {
            EnsureFrame();
            return new Query(_active, -1, -1, hostileOnly: true);
        }

        /// <summary>新生成及 AI 前调用；同帧新增实体及时补入，已有实体不重复登记。</summary>
        public static void Observe(Projectile projectile, ProjectileState state)
        {
            if (!_hasFrame || _frame != Main.GameUpdateCount || !projectile.active) return;
            Entry? old = _slots[projectile.whoAmI];
            if (old.HasValue && ReferenceEquals(old.Value.State, state)
                && old.Value.Identity == projectile.identity && old.Value.Owner == projectile.owner
                && old.Value.Type == projectile.type) return;

            Add(new Entry(projectile, state), ordered: false);
        }

        private static void EnsureFrame()
        {
            if (_hasFrame && _frame == Main.GameUpdateCount) return;
            _hasFrame = true;
            _frame = Main.GameUpdateCount;
            _active.Clear();
            foreach (List<Entry> entries in _owned.Values) entries.Clear();
            foreach (List<Entry> entries in _byType.Values) entries.Clear();
            Array.Clear(_slots, 0, _slots.Length);

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];
                if (projectile.active)
                    Add(new Entry(projectile, projectile.GetGlobalProjectile<ProjectileState>()), ordered: true);
            }
        }

        private static List<Entry> GetOwnedList(int owner, int type)
        {
            if (!_owned.TryGetValue((owner, type), out List<Entry> entries))
            {
                entries = new List<Entry>();
                _owned.Add((owner, type), entries);
            }
            return entries;
        }

        private static List<Entry> GetTypeList(int type)
        {
            if (!_byType.TryGetValue(type, out List<Entry> entries))
            {
                entries = new List<Entry>();
                _byType.Add(type, entries);
            }
            return entries;
        }

        private static void Add(Entry entry, bool ordered)
        {
            _slots[entry.Slot] = entry;
            Insert(_active, entry, ordered);
            Insert(GetOwnedList(entry.Owner, entry.Type), entry, ordered);
            Insert(GetTypeList(entry.Type), entry, ordered);
        }

        private static void Insert(List<Entry> entries, Entry entry, bool ordered)
        {
            if (ordered) { entries.Add(entry); return; }
            int low = 0, high = entries.Count;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (entries[middle].Slot < entry.Slot) low = middle + 1;
                else high = middle;
            }
            if (low < entries.Count && entries[low].Slot == entry.Slot) entries[low] = entry;
            else entries.Insert(low, entry);
        }

        public readonly struct Query
        {
            private readonly List<Entry> _entries;
            private readonly int _owner, _type;
            private readonly bool _hostileOnly;

            internal Query(List<Entry> entries, int owner, int type, bool hostileOnly)
            {
                _entries = entries;
                _owner = owner;
                _type = type;
                _hostileOnly = hostileOnly;
            }

            public Enumerator GetEnumerator() => new Enumerator(_entries, _owner, _type, _hostileOnly);

            public struct Enumerator
            {
                private readonly List<Entry> _entries;
                private readonly int _owner, _type;
                private readonly bool _hostileOnly;
                private int _position, _nextSlot;
                public Projectile Current { get; private set; }

                internal Enumerator(List<Entry> entries, int owner, int type, bool hostileOnly)
                {
                    _entries = entries;
                    _owner = owner;
                    _type = type;
                    _hostileOnly = hostileOnly;
                    _position = _nextSlot = 0;
                    Current = null;
                }

                public bool MoveNext()
                {
                    while (_position < _entries.Count)
                    {
                        Entry entry = _entries[_position++];
                        // 同帧生成可插入较小槽位，不能因此重复处理已经访问的实体。
                        if (entry.Slot < _nextSlot) continue;
                        _nextSlot = entry.Slot + 1;
                        if (!entry.TryResolve(out Projectile projectile)) continue;
                        if (_owner >= 0 && projectile.owner != _owner) continue;
                        if (_type >= 0 && projectile.type != _type) continue;
                        if (_hostileOnly && (!projectile.hostile || projectile.damage <= 0)) continue;
                        Current = projectile;
                        return true;
                    }
                    return false;
                }
            }
        }

        internal static void Clear()
        {
            _hasFrame = false;
            _active.Clear();
            _owned.Clear();
            _byType.Clear();
            Array.Clear(_slots, 0, _slots.Length);
        }
    }

    public class ProjectileLookupSystem : ModSystem
    {
        public override void OnWorldLoad() => ProjectileLookup.Clear();
        public override void OnWorldUnload() => ProjectileLookup.Clear();
        public override void Unload() => ProjectileLookup.Clear();
    }
}
