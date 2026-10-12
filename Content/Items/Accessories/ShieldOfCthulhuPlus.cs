using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Accessories
{
    public sealed class ShieldOfCthulhuPlus : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.EoCShield;

        public override void SetDefaults()
        {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.defense = 3;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
            Item.shieldSlot = ArmorIDs.Shield.ShieldofCthulhu;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) =>
            player.GetModPlayer<CompositeAccessoryPlayer>().EquipShield();

        public override void ModifyTooltips(List<TooltipLine> tooltips) => PanicToggleAccessory.AddDashHint(tooltips, Mod);

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.BrainOfConfusion).AddIngredient(ItemID.WormScarf)
            .AddIngredient(ItemID.EoCShield).AddIngredient(ItemID.ObsidianShield)
            .AddTile(TileID.TinkerersWorkbench).Register();
    }
}
