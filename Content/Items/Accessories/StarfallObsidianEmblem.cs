using Terraria;
using Terraria.ID;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Accessories
{
    public sealed class StarfallObsidianEmblem : PanicToggleAccessory
    {
        public override void SetDefaults()
        {
            Item.width = Item.height = 32;
            Item.accessory = true;
            Item.defense = 3;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) =>
            player.GetModPlayer<CompositeAccessoryPlayer>().EquipEmblem(this);

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.CharmofMyths).AddIngredient(ItemID.SweetheartNecklace)
            .AddIngredient(ItemID.StarVeil).AddIngredient(ItemID.ObsidianShield)
            .AddTile(TileID.TinkerersWorkbench).Register();
    }
}
