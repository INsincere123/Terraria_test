using Terraria;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    /// <summary>两个装备来源共享暗处遮盖免疫，不附加整屏光源。</summary>
    public static class TheDarkImmunityEffect
    {
        public const string DescriptionKey = "Mods.TestMod.EnvironmentEffects.TheDarkImmunity";

        public static void Apply(Player player)
        {
            if (HomewardJourneyCompatSystem.TheDarkCompatibilityReady)
                player.GetModPlayer<TheDarkImmunityPlayer>().Enabled = true;
        }
    }
}
