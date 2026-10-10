using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Compatibility;
using TestMod.Common.Players;
using TestMod.Content.Rarities;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem : Terraria.ModLoader.GlobalItem
    {
        private static bool IsGodMode(Player player) =>
            player?.active == true && player.GetModPlayer<GodModePlayer>().GodModeBuff;

        public override void SetDefaults(Item item)
        {
            if (item.type == ItemID.AmethystHook)
                item.shootSpeed = 25f;
            if (item.type == ItemID.DiamondHook)
                item.shootSpeed = 27f;
        }

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (line.Mod != "Terraria")
                return true;

            if (line.Name == "ItemName")
            {
                if (item.rare == ModContent.RarityType<TerraRarity>())
                    return !TextRenderingBridge.TryDrawTooltipLine(item, line, TestModTextStyles.RarityTerra);

                if (item.rare == ModContent.RarityType<EventHorizonRarity>())
                    return !TextRenderingBridge.TryDrawTooltipLine(item, line, TestModTextStyles.RarityEventHorizon);

                if (item.rare == ModContent.RarityType<AntaresRarity>())
                    return !TextRenderingBridge.TryDrawTooltipLine(item, line, TestModTextStyles.RarityAntares);

                return true;
            }

            if (line.Name == "Damage")
                return !TextRenderingBridge.TryDrawDamageLine(item, line);

            return true;
        }
    }
}
