using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalItems
{
    public sealed class PrefixAvailabilityGlobalItem : Terraria.ModLoader.GlobalItem
    {
        public override bool InstancePerEntity => true;
        private bool recorded;
        private int recordedPrefix;
        private bool effectsEnabled;

        internal void Record(Item item, bool enabled)
        {
            recorded = true;
            recordedPrefix = item.prefix;
            effectsEnabled = enabled;
        }

        internal void EnsureCurrent(Item item)
        {
            if (!PrefixAvailabilitySystem.IsRefinement(item.prefix))
                return;
            bool enabled = PrefixAvailabilitySystem.RefinementEnabled;
            if (recorded && recordedPrefix == item.prefix && effectsEnabled == enabled)
                return;
            if (!recorded && enabled)
                Record(item, true);
            else
                PrefixAvailabilitySystem.RefreshRefinement(item, enabled);
        }

        // 原版每帧克隆鼠标镜像时，GlobalItem 的默认 Clone 会保留以上记录，避免反复刷新。
        public override void UpdateInventory(Item item, Player player) => EnsureCurrent(item);
        public override void UpdateEquip(Item item, Player player) => EnsureCurrent(item);

        public override bool AllowPrefix(Item item, int pre) =>
            !PrefixAvailabilitySystem.IsRolling || PrefixAvailabilitySystem.IsEnabled(pre);

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            EnsureCurrent(item);
            if (!PrefixAvailabilitySystem.IsEnabled(item.prefix))
                tooltips.Add(new TooltipLine(Mod, "DisabledPrefix",
                    Language.GetTextValue("Mods.TestMod.PrefixSettings.Disabled")));
        }
    }
}
