using System;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Configs;
using TestMod.Common.GlobalItems;
using TestMod.Common.Utilities;
using TestMod.Content.Prefixes;

namespace TestMod.Common.Systems
{
    public sealed class PrefixAvailabilitySystem : ModSystem
    {
        [ThreadStatic] private static bool rolling;
        [ThreadStatic] private static bool refreshing;

        internal static bool RefinementEnabled => TestModServerConfig.Instance?.EnableRefinementPrefixes ?? true;
        internal static bool FusionEnabled => TestModServerConfig.Instance?.EnableFusionPrefix ?? true;
        internal static bool IsRolling => rolling;

        internal static bool IsRefinement(int prefix) => PrefixLoader.GetPrefix(prefix) is
            RefinementPrefix or RefinementMeleeSwingPrefix or RefinementMeleeOtherPrefix or
            RefinementRangedPrefix or RefinementRangedNoKBPrefix or RefinementMagicPrefix or
            RefinementMagicNoKBPrefix or RefinementSummonPrefix or RefinementSummonNoKBPrefix or RefinementWhipPrefix;

        internal static bool IsEnabled(int prefix) => IsRefinement(prefix) ? RefinementEnabled :
            PrefixLoader.GetPrefix(prefix) is FusionPrefix ? FusionEnabled : true;

        public override void Load() => On_Item.Prefix += ApplyPrefix;

        public override void PreUpdatePlayers()
        {
            if (!Main.dedServ)
            {
                UpdateHeldItem(Main.mouseItem);
                UpdateHeldItem(Main.reforgeItem);
            }
        }

        private static void UpdateHeldItem(Item item)
        {
            if (item != null && !item.IsAir && item.TryGetGlobalItem<PrefixAvailabilityGlobalItem>(out var state))
                state.EnsureCurrent(item);
        }

        public override void Unload()
        {
            On_Item.Prefix -= ApplyPrefix;
            rolling = refreshing = false;
            ItemStateUtils.Unload();
            TestModServerConfig.ClearInstance();
        }

        private static bool ApplyPrefix(On_Item.orig_Prefix original, Item item, int prefix)
        {
            bool previous = rolling;
            // 正数也用于读档/网络接收：允许恢复名称，随后去掉禁用的属性。
            // 新获取的随机词条在 AllowPrefix 中拒绝；批量券另行检查当前配置。
            rolling = previous || prefix == -1 || prefix == -2;
            bool applied;
            try
            {
                applied = original(item, prefix);
            }
            finally
            {
                rolling = previous;
            }
            if (applied && prefix != -3 && !refreshing && IsRefinement(item.prefix))
            {
                if (!RefinementEnabled)
                    RefreshRefinement(item, false);
                else if (item.TryGetGlobalItem<PrefixAvailabilityGlobalItem>(out var state))
                    state.Record(item, true);
            }
            return applied;
        }

        internal static void RefreshRefinement(Item item, bool enabled)
        {
            if (refreshing || item.IsAir || !IsRefinement(item.prefix))
                return;
            refreshing = true;
            try
            {
                // 禁用时保留前缀名称、价格和稀有度；只恢复武器的基础战斗属性。
                ItemStateUtils.RefreshPreservingInstances(item, enabled);
                if (item.TryGetGlobalItem<PrefixAvailabilityGlobalItem>(out var state))
                    state.Record(item, enabled);
            }
            finally
            {
                refreshing = false;
            }
        }
    }
}
