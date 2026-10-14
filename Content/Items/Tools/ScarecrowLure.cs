using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Tools
{
    public sealed class ScarecrowLure : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.TargetDummy;
        public override void SetDefaults()
        {
            Item.width = 26; Item.height = 36;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = ScarecrowSystem.UseTicks;
            Item.noMelee = true; Item.autoReuse = false;
            Item.maxStack = Item.CommonMaxStack; Item.consumable = true;
            Item.rare = ItemRarityID.Blue; Item.UseSound = SoundID.Item44;
        }
        public override bool CanUseItem(Player player) => DecoySystem.Ready;
        public override bool? CanAutoReuseItem(Player player) => false;
        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer) ScarecrowSystem.RequestPlace(player, Main.MouseWorld);
            return true;
        }
        public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.TargetDummy)
            .AddIngredient(ItemID.Mannequin).AddIngredient(ItemID.RainbowFlare,100).AddIngredient(ItemID.ShimmerFlare,100)
            .AddTile(TileID.WorkBenches).Register();
    }
}
