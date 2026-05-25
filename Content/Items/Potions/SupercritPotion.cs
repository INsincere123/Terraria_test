using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Potions
{
    public class SupercritPotion : ModItem
    {
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
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.value = Item.buyPrice(gold: 7);
        }

        public override bool? UseItem(Player player)
        {
            // 15 分钟 = 15 * 60 * 60 = 54000 帧
            player.AddBuff(ModContent.BuffType<SupercritBuff>(), 54000);
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe(50)
                .AddIngredient(ItemID.EyeoftheGolem)
                .AddIngredient(ItemID.BottledWater, 50)
                .AddTile(TileID.Bottles)
                .AddTile(TileID.AlchemyTable)
                .Register();
        }
    }
}
