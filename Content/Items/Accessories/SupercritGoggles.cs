using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Accessories
{
    public class SupercritGoggles : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.value = Item.buyPrice(gold: 88);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetCritChance<GenericDamageClass>() += 25f;
            player.AddBuff(ModContent.BuffType<SupercritBuff>(), 2);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.EyeoftheGolem, 3)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}