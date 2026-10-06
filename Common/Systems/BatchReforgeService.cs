using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using TestMod.Content.Items;
using TestMod.Common.Utilities;

namespace TestMod.Common.Systems
{
    internal enum BatchReforgeKind
    {
        Accessory,
        Weapon
    }

    // 仅 owner 客户端修改自己的物品；现有背包/装备/模组槽差异同步负责传递。
    internal static class BatchReforgeService
    {
        internal const int HighestPrefix = -1;
        // 原版 inventory[58] 是 Main.mouseItem 的逐帧克隆，不属于实际背包存储。
        private const int MouseItemSlot = 58;
        internal sealed class Slot
        {
            private readonly List<(Item[] Items, int Index)> locations = new();
            internal Item Item => locations[0].Items[locations[0].Index];
            internal Slot(Item[] items, int index) => Add(items, index);
            internal void Add(Item[] items, int index) => locations.Add((items, index));
            internal bool References(Item item) => locations.TrueForAll(location =>
                ReferenceEquals(location.Items[location.Index], item));
            internal void Replace(Item item)
            {
                foreach (var location in locations)
                    location.Items[location.Index] = item;
            }
        }

        internal readonly record struct Counts(int Changed, int Matching, int Skipped);
        internal readonly record struct Option(int Prefix, Item Preview);

        private static FieldInfo accessorySlots;
        private static FieldInfo loadouts;
        private static PropertyInfo loadoutAccessories;
        private static bool initialized;
        private static bool warned;

        internal static void Unload()
        {
            accessorySlots = loadouts = null;
            loadoutAccessories = null;
            initialized = warned = false;
        }

        private static void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = typeof(ModAccessorySlotPlayer);
            accessorySlots = type.GetField("exAccessorySlot", flags);
            loadouts = type.GetField("exLoadouts", flags);
            Type element = loadouts?.FieldType.GetElementType();
            loadoutAccessories = element?.GetProperty("ExAccessorySlot", flags);
        }

        private static void Warn(Exception exception)
        {
            if (warned)
                return;
            warned = true;
            ModContent.GetInstance<TestMod>().Logger.Warn($"[BatchReforge] {exception}");
        }

        internal static bool TryCollect(Player player, BatchReforgeKind kind, out List<Slot> slots)
        {
            slots = new List<Slot>();
            var seen = new Dictionary<Item, Slot>(ReferenceEqualityComparer.Instance);
            var collected = slots;
            void Add(Item[] items, int length = -1)
            {
                if (items == null)
                    throw new InvalidOperationException("Missing accessory storage.");
                int count = length < 0 ? items.Length : Math.Min(length, items.Length);
                for (int i = 0; i < count; i++)
                {
                    Item item = items[i];
                    if (item == null || item.IsAir || !IsTarget(item, kind))
                        continue;
                    if (seen.TryGetValue(item, out Slot shared))
                        shared.Add(items, i);
                    else
                    {
                        var slot = new Slot(items, i);
                        seen.Add(item, slot);
                        collected.Add(slot);
                    }
                }
            }

            try
            {
                Add(player.inventory, MouseItemSlot);
                // 武器仅处理背包，不读取原版/模组配装的内部存储。
                if (kind == BatchReforgeKind.Weapon)
                    return true;
                Add(player.armor);
                // 当前配装的物品在 armor；其他配装保存在 Loadouts。
                foreach (var loadout in player.Loadouts)
                    Add(loadout.Armor);

                var modSlots = player.GetModPlayer<ModAccessorySlotPlayer>();
                if (modSlots.SlotCount > 0)
                {
                    Initialize();
                    if (accessorySlots == null || loadouts == null || loadoutAccessories == null)
                        throw new MissingMemberException("tModLoader accessory loadout storage changed.");
                    Add((Item[])accessorySlots.GetValue(modSlots));
                    if (loadouts.GetValue(modSlots) is not Array stored || stored.Length != player.Loadouts.Length)
                        throw new InvalidOperationException("Invalid modded accessory loadouts.");
                    foreach (object loadout in stored)
                        Add((Item[])loadoutAccessories.GetValue(loadout));
                }
                return true;
            }
            catch (Exception exception)
            {
                Warn(exception);
                slots.Clear();
                return false;
            }
        }

        private static bool IsTarget(Item item, BatchReforgeKind kind) =>
            kind == BatchReforgeKind.Accessory ? item.accessory :
                !item.accessory && item.ammo == 0 &&
                (item.damage > 0 || item.pick > 0 || item.axe > 0 || item.hammer > 0);

        private static List<int> Prefixes(BatchReforgeKind kind)
        {
            var prefixes = new List<int>();
            for (int i = 1; i < PrefixID.Count; i++)
            {
                bool accessory = i >= PrefixID.Hard && i <= PrefixID.Violent;
                if (accessory == (kind == BatchReforgeKind.Accessory))
                    prefixes.Add(i);
            }
            for (int i = PrefixID.Count; i < PrefixLoader.PrefixCount; i++)
            {
                ModPrefix prefix = PrefixLoader.GetPrefix(i);
                if (prefix != null && PrefixAvailabilitySystem.IsEnabled(i) &&
                    (prefix.Category == PrefixCategory.Accessory) == (kind == BatchReforgeKind.Accessory))
                    prefixes.Add(i);
            }
            return prefixes;
        }

        private static Item UnprefixedClone(Item original)
        {
            Item candidate = original.Clone();
            ResetPrefixPreservingInstances(candidate);
            return candidate;
        }

        private static void ResetPrefixPreservingInstances(Item candidate)
        {
            ItemStateUtils.ResetPrefixPreservingInstances(candidate);
        }

        private static bool TryHighest(Item original, List<int> prefixes, out int bestPrefix)
        {
            bestPrefix = 0;
            try
            {
                // 每个候选从同一无前缀克隆出发，避免叠加旧前缀价值或重复序列化。
                Item clean = UnprefixedClone(original);
                int bestValue = int.MinValue;
                foreach (int prefix in prefixes)
                {
                    if (!TryPrepare(clean, prefix, false, out Item candidate))
                        continue;
                    bool preferTie = prefix == original.prefix ||
                        (bestPrefix != original.prefix && prefix < bestPrefix);
                    if (bestPrefix == 0 || candidate.value > bestValue ||
                        (candidate.value == bestValue && preferTie))
                    {
                        bestValue = candidate.value;
                        bestPrefix = prefix;
                    }
                }
                return bestPrefix > 0;
            }
            catch (Exception exception)
            {
                Warn(exception);
                return false;
            }
        }

        private static bool TryPrepare(Item original, int prefix, bool reforge, out Item result)
        {
            result = null;
            Item previous = Main.reforgeItem;
            try
            {
                if (!PrefixAvailabilitySystem.IsEnabled(prefix))
                    return false;
                Item candidate = original.Clone();
                if (reforge)
                    Main.reforgeItem = candidate;
                if (!ItemLoader.CanReforge(candidate))
                    return false;
                if (reforge)
                    ItemLoader.PreReforge(candidate);
                ResetPrefixPreservingInstances(candidate);
                if (!candidate.CanRollPrefix(prefix) || !candidate.CanApplyPrefix(prefix) || !candidate.Prefix(prefix))
                    return false;
                if (reforge)
                {
                    ItemLoader.PostReforge(candidate);
                }
                if (candidate.prefix != prefix || candidate.type != original.type || candidate.IsAir)
                    return false;
                candidate.stack = original.stack;
                candidate.favorited = original.favorited;
                result = candidate;
                return true;
            }
            catch (Exception exception)
            {
                Warn(exception);
                return false;
            }
            finally
            {
                if (reforge)
                    Main.reforgeItem = previous;
            }
        }

        internal static bool TryOptions(Player player, BatchReforgeKind kind, out List<Option> options)
        {
            options = new List<Option>();
            if (!TryCollect(player, kind, out var slots))
                return false;
            if (kind == BatchReforgeKind.Weapon)
                options.Add(new Option(HighestPrefix, null));
            foreach (int prefix in Prefixes(kind))
                foreach (Slot slot in slots)
                    if (TryPrepare(slot.Item, prefix, false, out Item preview))
                    {
                        options.Add(new Option(prefix, preview));
                        break;
                    }
            return true;
        }

        internal static bool TryCounts(Player player, BatchReforgeKind kind, int prefix, out Counts counts)
        {
            counts = default;
            if (!TryCollect(player, kind, out var slots))
                return false;
            var prefixes = Prefixes(kind);
            int changed = 0, matching = 0, skipped = 0;
            foreach (Slot slot in slots)
            {
                int target = prefix;
                if (prefix == HighestPrefix && !TryHighest(slot.Item, prefixes, out target))
                    skipped++;
                else if (slot.Item.prefix == target)
                    matching++;
                else if (TryPrepare(slot.Item, target, false, out _))
                    changed++;
                else
                    skipped++;
            }
            counts = new Counts(changed, matching, skipped);
            return true;
        }

        internal static string Execute(Player player, BatchReforgeKind kind, int prefix, out Counts counts)
        {
            counts = default;
            if (Main.dedServ || player.whoAmI != Main.myPlayer || player.dead || player.ItemAnimationActive)
                return "Unavailable";
            int scrollType = kind == BatchReforgeKind.Weapon
                ? ModContent.ItemType<WeaponReforgeScroll>() : ModContent.ItemType<AccessoryReforgeScroll>();
            int scrollSlot = Array.FindIndex(player.inventory, 0, Math.Min(MouseItemSlot, player.inventory.Length),
                item => item.type == scrollType && item.stack > 0);
            bool fromMouse = scrollSlot < 0;
            Item scroll = fromMouse ? Main.mouseItem : player.inventory[scrollSlot];
            if (scroll == null || scroll.type != scrollType || scroll.stack <= 0)
                return "NoScroll";
            var prefixes = Prefixes(kind);
            bool valid = (kind == BatchReforgeKind.Weapon && prefix == HighestPrefix) || prefixes.Contains(prefix);
            if (!valid)
                return "SelectPrefix";
            if (!TryCollect(player, kind, out var slots))
                return "StorageError";

            var changes = new List<(Slot Slot, Item Original, Item Result)>();
            int matching = 0, skipped = 0;
            foreach (Slot slot in slots)
            {
                Item original = slot.Item;
                int target = prefix;
                if (prefix == HighestPrefix && !TryHighest(original, prefixes, out target))
                    skipped++;
                else if (original.prefix == target)
                    matching++;
                else if (TryPrepare(original, target, true, out Item result))
                    changes.Add((slot, original, result));
                else
                    skipped++;
            }

            // 所有结果准备完成后才提交；异常项不会清掉原前缀，也不会单独扣券。
            counts = new Counts(changes.Count, matching, skipped);
            if (changes.Count == 0)
                return "NoChanges";
            Item currentScroll = fromMouse ? Main.mouseItem : player.inventory[scrollSlot];
            if (!ReferenceEquals(currentScroll, scroll) || scroll.type != scrollType || scroll.stack <= 0)
                return "NoScroll";
            foreach (var change in changes)
                if (!change.Slot.References(change.Original))
                    return "Unavailable";
            foreach (var change in changes)
            {
                change.Slot.Replace(change.Result);
                change.Result.NetStateChanged();
            }
            scroll.stack--;
            if (scroll.stack <= 0)
                scroll.TurnToAir();
            scroll.NetStateChanged();
            if (fromMouse)
            {
                // 扣除真实鼠标物品后刷新镜像；原版下一帧 DropItemCheck 再克隆也不会恢复数量。
                player.inventory[MouseItemSlot] = scroll.Clone();
                player.inventory[MouseItemSlot].NetStateChanged();
            }
            return null;
        }
    }
}
