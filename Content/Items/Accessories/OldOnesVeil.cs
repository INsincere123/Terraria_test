using Terraria;
using Terraria.ID;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Accessories
{
    public sealed class OldOnesVeil : PanicToggleAccessory
    {
        protected override bool HasDash => true;
        public override void SetDefaults()
        {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.defense = 7;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 15);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) =>
            player.GetModPlayer<CompositeAccessoryPlayer>().EquipVeil(this);

        public override void AddRecipes()
        {
            CreateRecipe().AddIngredient(ItemID.CharmofMyths).AddIngredient(ItemID.SweetheartNecklace)
                .AddIngredient(ItemID.StarVeil).AddIngredient(ItemID.AnkhShield)
                .AddIngredient<ShieldOfCthulhuPlus>().AddIngredient(ItemID.MasterNinjaGear)
                .AddTile(TileID.TinkerersWorkbench).Register();
            CreateRecipe().AddIngredient<StarfallObsidianEmblem>().AddIngredient(ItemID.AnkhShield)
                .AddIngredient<ShieldOfCthulhuPlus>().AddIngredient(ItemID.MasterNinjaGear)
                .AddTile(TileID.TinkerersWorkbench).Register();
        }
    }
}
