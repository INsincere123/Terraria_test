using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Accessories
{
    public sealed class MomentumConverter : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
            => player.GetModPlayer<MomentumConverterPlayer>().Equipped = true;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string key = KeybindUtils.GetKeyText(MomentumConverterKeybind.ActivateKey);
            tooltips.Add(new TooltipLine(Mod, "MomentumConverterActivation",
                Language.GetTextValue("Mods.TestMod.Items.MomentumConverter.ActivationHint", key)));
        }

        public override void AddRecipes()
            => CreateRecipe()
                .AddIngredient(ItemID.HallowedBar, 10)
                .AddIngredient(ItemID.SoulofFlight, 10)
                .AddIngredient(ItemID.Wire, 50)
                .AddTile(TileID.MythrilAnvil)
                .Register();
    }
}
