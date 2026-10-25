using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.AccessoryEffects;
using TestMod.Content.Items.Armor;

namespace TestMod.Common.Systems
{
    // 只缓存显示字符串；装备标志和效果仍由 UpdateArmorSet 每 tick 注入。
    public class UltimateArmorSetBonusSystem : ModSystem
    {
        private static readonly string[] Texts = new string[4];
        private static string cachedKey;
        private static GameCulture cachedCulture;
        private static bool cachedCalamity, cachedJourney, cachedDefenseImmunity;

        public override void OnLocalizationsLoaded() => Clear();
        public override void Unload() => Clear();

        private static void Clear()
        {
            System.Array.Clear(Texts);
            cachedKey = null;
            cachedCulture = null;
        }

        internal static string GetText(bool dragonEnabled, bool cannonEnabled)
        {
            var keys = AntaresKeybinds.GravityWellKey.GetAssignedKeys();
            string key = keys.Count > 0 ? keys[0] : Language.GetTextValue("Mods.TestMod.Common.Unbound");
            bool calamity = CalamityCompatSystem.CalamityLoaded;
            bool journey = HomewardJourneyCompatSystem.TheDarkCompatibilityReady;
            bool defenseImmunity = CalamityDefenseDamageSystem.ImmunityReady;
            if (cachedKey != key || cachedCulture != Language.ActiveCulture ||
                cachedCalamity != calamity || cachedJourney != journey || cachedDefenseImmunity != defenseImmunity)
            {
                Clear();
                cachedKey = key;
                cachedCulture = Language.ActiveCulture;
                cachedCalamity = calamity;
                cachedJourney = journey;
                cachedDefenseImmunity = defenseImmunity;
            }

            int index = (dragonEnabled ? 1 : 0) | (cannonEnabled ? 2 : 0);
            if (Texts[index] != null) return Texts[index];

            string calamityBonus = calamity
                ? Language.GetTextValue("Mods.TestMod.Items.UltimateHelmet.CalamityBonus") + Language.GetTextValue(AbyssImmunityEffect.DescriptionKey)
                : "";
            string journeyBonus = journey ? "\n" + Language.GetTextValue(TheDarkImmunityEffect.DescriptionKey) : "";
            string dragonStatus = Language.GetTextValue("Mods.TestMod.Items.UltimateHelmet.DragonState",
                ToggleableArmorItem.GetEffectStateText(dragonEnabled));
            string cannonStatus = Language.GetTextValue("Mods.TestMod.Items.UltimateHelmet.CannonState",
                ToggleableArmorItem.GetEffectStateText(cannonEnabled));
            string text = Language.GetTextValue("Mods.TestMod.Items.UltimateHelmet.SetBonus",
                key, calamityBonus, journeyBonus, dragonStatus, cannonStatus);
            if (defenseImmunity) text += "\n" + Language.GetTextValue(CalamityDefenseDamageSystem.ImmunityTextKey);
            return Texts[index] = text;
        }
    }
}
