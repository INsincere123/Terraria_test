using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Projectiles;
using TestMod.Rarities;

namespace TestMod.Items.Weapons
{
    public class TestWeapon : ModItem
    {
        public override string Texture => "Terraria/Images/Item_4952";

        public const float ShootSpeed = 10f;

        public override void SetStaticDefaults()
        {
            Item.staff[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.damage = 70;
            Item.DamageType = DamageClass.Magic;
            Item.width = 40;
            Item.height = 40;
            Item.mana = 8;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 3f;
            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ModContent.RarityType<EventHorizonRarity>();
            Item.UseSound = SoundID.Item117;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<TestProjectile>();
            Item.shootSpeed = ShootSpeed;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 direction = Main.MouseWorld - player.Center;
            if (direction.LengthSquared() <= 0.001f)
                direction = Vector2.UnitX * player.direction;

            Vector2 normalizedDirection = direction.SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(
                source,
                player.Center + normalizedDirection * 42f,
                normalizedDirection * ShootSpeed,
                type,
                damage,
                knockback,
                player.whoAmI);

            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.DirtBlock)
                .Register();
        }
    }
}
