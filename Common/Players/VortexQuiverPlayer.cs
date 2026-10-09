using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.Players
{
    public sealed class VortexQuiverPlayer : ModPlayer
    {
        // 保留原 ModPlayer 名称；三个类别各自维护装备、模式与游标。
        private readonly AmmoCycle[] cycles = { new(), new(), new() };

        private sealed class AmmoCycle
        {
            internal bool Equipped;
            internal VortexAmmoAccessory Item;
            internal VortexAmmoMode Mode;
            internal int LastRank = -1;
            internal Item PendingRandom;
            internal void ResetCursor() { LastRank = -1; PendingRandom = null; }
            internal void Clear() { Equipped = false; Item = null; ResetCursor(); }
        }

        public override void ResetEffects()
        {
            foreach (AmmoCycle cycle in cycles) cycle.Equipped = false;
        }

        internal bool Equip(VortexAmmoAccessory item)
        {
            AmmoCycle cycle = GetCycle(item.AmmoCategory);
            if (cycle == null || cycle.Equipped) return false;
            if (!ReferenceEquals(cycle.Item, item) || cycle.Mode != item.Mode) cycle.ResetCursor();
            cycle.Item = item;
            cycle.Mode = item.Mode;
            cycle.Equipped = true;
            return true;
        }

        private AmmoCycle GetCycle(int category) => category == AmmoID.Arrow ? cycles[0] :
            category == AmmoID.Bullet ? cycles[1] : category == AmmoID.Rocket ? cycles[2] : null;
        internal bool IsEquipped(int category) => GetCycle(category)?.Equipped == true;
        internal bool IsCycling(int category) => GetCycle(category) is { Equipped: true, Mode: not VortexAmmoMode.Simple };

        public override void PostUpdateEquips()
        {
            foreach (AmmoCycle cycle in cycles)
                if (!cycle.Equipped) cycle.Clear();
        }

        public override void UpdateDead() => ClearSession();
        public override void OnEnterWorld() => ClearSession();
        internal void ClearSession()
        {
            foreach (AmmoCycle cycle in cycles) cycle.Clear();
        }

        // rank 使用固定库存次序而非候选列表下标，弹药耗尽不会跳过下一堆。
        internal Item SelectAmmo(Item weapon, out int selectedRank)
        {
            selectedRank = -1;
            AmmoCycle cycle = GetCycle(weapon.useAmmo);
            if (cycle == null || !cycle.Equipped || cycle.Mode == VortexAmmoMode.Simple) return null;
            Span<int> ranks = stackalloc int[58];
            int count = 0;
            int limit = cycle.Mode == VortexAmmoMode.Default ? 4 : 58;
            for (int rank = 0; rank < limit; rank++)
            {
                Item ammo = Player.inventory[SlotForRank(rank)];
                if (!ammo.IsAir && ammo.stack > 0 && ItemLoader.CanChooseAmmo(weapon, ammo, Player))
                    ranks[count++] = rank;
            }
            if (count == 0) return null;
            selectedRank = ranks[0];
            if (cycle.Mode == VortexAmmoMode.Random)
            {
                // 同一次待发射选择的查询和预览稳定；候选失效时才重抽。
                for (int i = 0; i < count; i++)
                    if (ReferenceEquals(Player.inventory[SlotForRank(ranks[i])], cycle.PendingRandom))
                    {
                        selectedRank = ranks[i];
                        return cycle.PendingRandom;
                    }
                selectedRank = ranks[Main.rand.Next(count)];
                cycle.PendingRandom = Player.inventory[SlotForRank(selectedRank)];
            }
            else
            {
                for (int i = 0; i < count; i++)
                    if (ranks[i] > cycle.LastRank)
                    {
                        selectedRank = ranks[i];
                        break;
                    }
            }
            return Player.inventory[SlotForRank(selectedRank)];
        }

        internal void CommitSelection(int ammoCategory, int rank)
        {
            AmmoCycle cycle = GetCycle(ammoCategory);
            if (rank < 0 || cycle == null || !cycle.Equipped) return;
            cycle.LastRank = rank;
            cycle.PendingRandom = null;
        }

        private static int SlotForRank(int rank) => rank < 4 ? rank + 54 : rank - 4;
    }
}
