using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Utilities
{
    internal static class ItemStateUtils
    {
        private static PropertyInfo modItem;
        private static FieldInfo globals;

        internal static void Unload()
        {
            modItem = null;
            globals = null;
        }

        private static void Refresh(Item item, bool applyPrefix, bool preservePrefixMetadata)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            modItem ??= typeof(Item).GetProperty(nameof(Item.ModItem), flags);
            globals ??= typeof(Item).GetField("_globals", flags);
            if (modItem?.SetMethod == null || globals == null)
                throw new MissingMemberException("tModLoader item instance storage changed.");
            ModItem preservedModItem = item.ModItem;
            object preservedGlobals = globals.GetValue(item);
            int prefix = item.prefix, value = item.value, rare = item.rare;
            if (!applyPrefix)
                item.prefix = 0;
            try
            {
                item.Refresh(false);
            }
            finally
            {
                modItem.SetValue(item, preservedModItem);
                globals.SetValue(item, preservedGlobals);
                if (preservePrefixMetadata)
                {
                    item.prefix = prefix;
                    item.value = value;
                    item.rare = rare;
                }
            }
        }

        internal static void ResetPrefixPreservingInstances(Item item)
        {
            if (item.prefix != 0)
                Refresh(item, false, false);
        }

        internal static void RefreshPreservingInstances(Item item, bool applyPrefix) =>
            Refresh(item, applyPrefix, true);
    }
}
