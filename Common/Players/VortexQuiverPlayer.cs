using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.Players
{
    public sealed class VortexQuiverPlayer : ModPlayer
    {
        // 保留原 ModPlayer 名称；三个类别各自维护装备、模式与游标。
        private readonly AmmoCycle[] cycles = { new(), new(), new() };
        private RangerSeal rangerSeal;

        private sealed class AmmoCycle
        {
            internal bool Equipped;
            internal bool VortexEquipped;
            internal VortexAmmoAccessory Item;
            internal ModItem ModeSource;
            internal VortexAmmoMode Mode;
            internal int LastRank = -1;
            internal Item PendingRandom;
            internal void ResetCursor() { LastRank = -1; PendingRandom = null; }
            internal void Clear() { Equipped = VortexEquipped = false; Item = null; ModeSource = null; ResetCursor(); }
        }

        public override void ResetEffects()
        {
            rangerSeal = null;
            foreach (AmmoCycle cycle in cycles)
            {
                cycle.Equipped = cycle.VortexEquipped = false;
                cycle.Item = null;
            }
        }

        internal bool Equip(VortexAmmoAccessory item)
        {
            AmmoCycle cycle = GetCycle(item.AmmoCategory);
            if (cycle == null || cycle.VortexEquipped) return false;
            cycle.Item = item;
            cycle.Equipped = cycle.VortexEquipped = true;
            return true;
        }

        // 装备效果独立收集，轮换来源在全部装备更新后统一选定，不受槽位顺序影响。
        internal bool EquipRangerSeal(RangerSeal item)
        {
            if (rangerSeal != null) return false;
            rangerSeal = item;
            foreach (AmmoCycle cycle in cycles)
                cycle.Equipped = true;
            return true;
        }

        private AmmoCycle GetCycle(int category) => category == AmmoID.Arrow ? cycles[0] :
            category == AmmoID.Bullet ? cycles[1] : category == AmmoID.Rocket ? cycles[2] : null;
        // 此查询只代表星璇来源，不能把轮换可用误当作星璇强化。
        internal bool IsEquipped(int category) => GetCycle(category)?.VortexEquipped == true;
        // 射手之证独立省弹判定：装备即对所有弹药类别生效，不依赖类别轮换。
        internal bool HasRangerSeal => rangerSeal != null;
        internal bool IsRangerSeal(int category) => rangerSeal != null && GetCycle(category) != null;
        internal bool IsCycling(int category) => GetCycle(category) is { Equipped: true, Mode: not VortexAmmoMode.Simple };

        // 射手之证切换模式：仅更新由这件物品控制的类别，不修改同类别星璇模式。
        internal void SetRangerSealMode(RangerSeal item)
        {
            foreach (AmmoCycle cycle in cycles)
                if (cycle.Equipped && ReferenceEquals(cycle.ModeSource, item))
                {
                    if (cycle.Mode != item.Mode) cycle.ResetCursor();
                    cycle.Mode = item.Mode;
                }
        }

        public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
        {
            // 弹幕槽位只在本地受伤入口解析。Kill 内的爆炸可能已将 active 清空，不能据此排除。
            int slot = damageSource.SourceProjectileLocalIndex;
            if (Player.whoAmI != Main.myPlayer || Player.dead || slot < 0 || slot >= Main.maxProjectiles ||
                damageSource.SourceNPCIndex >= 0 || damageSource.SourceOtherIndex >= 0 ||
                (damageSource.SourcePlayerIndex >= 0 && damageSource.SourcePlayerIndex != Player.whoAmI))
                return false;
            Projectile projectile = Main.projectile[slot];
            return projectile.owner == Player.whoAmI && projectile.type == damageSource.SourceProjectileType &&
                projectile.GetGlobalProjectile<global::TestMod.Common.GlobalProjectiles.GlobalProjectile>().VortexProtectsOwner(projectile);
        }

        public override void PostUpdateEquips()
        {
            foreach (AmmoCycle cycle in cycles)
            {
                if (!cycle.Equipped) { cycle.Clear(); continue; }
                // 同类别星璇优先控制模式；射手之证继续提供本身的全部效果。
                ModItem source = cycle.Item ?? (ModItem)rangerSeal;
                VortexAmmoMode mode = cycle.Item?.Mode ?? rangerSeal.Mode;
                if (!ReferenceEquals(cycle.ModeSource, source) || cycle.Mode != mode) cycle.ResetCursor();
                cycle.ModeSource = source;
                cycle.Mode = mode;
            }
        }

        public override void UpdateDead() => ClearSession();
        public override void OnEnterWorld() => ClearSession();
        internal void ClearSession()
        {
            rangerSeal = null;
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
