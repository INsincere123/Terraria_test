using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Content.Rarities;
using TestMod.Common.Players;
using TestMod.Common.Mechanics.AccessoryEffects;

namespace TestMod.Content.Items.Accessories
{
    public class AdversityAmulet : ModItem
    {
        // 自定义图标：Content/Items/Accessories/AdversityAmulet.png（32x32，alpha 透明底）

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.value = Item.sellPrice(gold: 45);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<AdversityAmuletPlayer>().HasAdversityAmulet = true;
            DebuffImmunityEffect.ApplyAll(player);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.AnkhCharm)
                .AddIngredient(ItemID.LunarBar, 20)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            var adaptation = Main.LocalPlayer.GetModPlayer<AdversityAmuletPlayer>();
            tooltips.Add(new TooltipLine(Mod, "AdversityAdaptationState",
                Language.GetTextValue("Mods.TestMod.Items.AdversityAmulet.AdaptationState",
                    adaptation.Stacks, AdversityAmuletPlayer.MaxStacks,
                    (int)System.MathF.Round(adaptation.AdaptationReduction * 100f),
                    adaptation.RemainingSeconds)));
        }
    }
}
