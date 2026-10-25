using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Accessories
{
    public class SmartScope : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.LightRed;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
            => player.GetModPlayer<SmartScopePlayer>().Equipped = true;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "Activation", Language.GetTextValue(
                "Mods.TestMod.Items.SmartScope.Activation", KeybindUtils.GetKeyText(SmartScopeKeybind.ActivateKey))));
            if (ModLoader.HasMod("CalamityMod"))
                tooltips.Add(new TooltipLine(Mod, "CalamityCompatibility", Language.GetTextValue(
                    "Mods.TestMod.Items.ClassicGrapeBeer.CalamityCompatibility")));
        }
    }
}
