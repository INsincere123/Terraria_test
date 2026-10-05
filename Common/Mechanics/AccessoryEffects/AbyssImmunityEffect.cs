using Terraria;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    /// <summary>共享装备入口；保留真实深渊判定，只屏蔽灾厄的环境结算。</summary>
    public static class AbyssImmunityEffect
    {
        public const string DescriptionKey = "Mods.TestMod.EnvironmentEffects.AbyssImmunity";

        public static void Apply(Player player)
        {
            if (CalamityCompatSystem.CalamityLoaded)
                player.GetModPlayer<AbyssImmunityPlayer>().Enabled = true;
        }
    }
}
