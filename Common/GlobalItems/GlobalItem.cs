using System.Collections.ObjectModel;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Rarities;

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

            if (item.type == ItemID.StormTigerStaff)
            {
                item.damage = 51;
                item.knockBack = 10;
                item.useTime = 20;
                item.useAnimation = 20;
            }

            if (item.type == ItemID.AbigailsFlower)
            {
                item.damage = 14;
                item.knockBack = 2;
                item.mana = 0;
                item.useTime = 20;
                item.useAnimation = 20;
            }
        }

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (item.rare == ModContent.RarityType<EventHorizonRarity>())
                return true;

            if (line.Mod != "Terraria")
                return true;

            if (line.Name == "ItemName")
            {
                if (item.rare == ModContent.RarityType<AntaresRarity>())
                {
                    AntaresRarity.Draw(item, line);
                    return false;
                }

                return true;
            }

            if (line.Name == "Damage")
                return !DamageLineRenderer.TryDraw(item, line);

            return true;
        }

        public override bool PreDrawTooltip(Item item, ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y)
        {
            if (item.rare == ModContent.RarityType<EventHorizonRarity>())
                return false;

            return true;
        }

        public override void PostDrawTooltip(Item item, ReadOnlyCollection<DrawableTooltipLine> lines)
        {
            if (item.rare == ModContent.RarityType<EventHorizonRarity>())
                EventHorizonRarity.DrawTooltip(item, lines);
        }
    }
}
