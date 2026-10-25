using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Potions
{
    public class SupercritPotion : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 20;
        }

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 32;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.UseSound = SoundID.Item3;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = true;
            Item.autoReuse = false;
            Item.consumable = true;
            Item.maxStack = 9999;
            Item.buffType = ModContent.BuffType<SupercritBuff>();
            Item.buffTime = 15 * 60 * 60;
            Item.rare = ModContent.RarityType<TerraRarity>();
            Item.value = Item.buyPrice(gold: 3);
        }

        public override void AddRecipes()
        {
            CreateRecipe(33)
                .AddIngredient(ItemID.EyeoftheGolem)
                .AddIngredient(ItemID.BottledWater, 99)
                .AddTile(TileID.Bottles)
                .AddTile(TileID.AlchemyTable)
                .Register();
        }
    }
}
