using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Projectiles.Accessories;

namespace TestMod.Content.Items.Accessories
{
    public class EnhancedStaticHook : ModItem
    {
        public const float Reach = 1200f;
        public const float LaunchSpeed = 32f;
        public const float MovementSpeed = 12f;
        public const float RetreatSpeed = 48f;

        public override string Texture => "Terraria/Images/Item_" + ItemID.StaticHook;

        public override void SetDefaults()
        {
            Item.CloneDefaults(ItemID.StaticHook);
            Item.shoot = ModContent.ProjectileType<EnhancedStaticHookProj>();
            Item.shootSpeed = LaunchSpeed;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.StaticHook)
                .AddIngredient(ItemID.LunarBar, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
