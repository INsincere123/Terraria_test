using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;
using TestMod.Projectiles.Minions;
using TestMod.Rarities;

namespace TestMod.Items.Weapons.Summon
{
    public class BlackHoleStaff : ModItem
    {
        private const int MaxBlackHoles = 4;

        public override string Texture => "Terraria/Images/Item_4952";

        public override void SetStaticDefaults()
        {
            ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
        }

        public override void SetDefaults()
        {
            Item.width = 42;
            Item.height = 42;
            Item.damage = 800;
            Item.mana = 0;
            Item.useAnimation = 24;
            Item.useTime = 24;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 60f;
            Item.value = Item.sellPrice(platinum: 10);
            Item.rare = ModContent.RarityType<EventHorizonRarity>();
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.Summon;
            Item.buffType = ModContent.BuffType<BlackHoleMinionBuff>();
            Item.shoot = ModContent.ProjectileType<BlackHoleMinion>();
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[Item.shoot] < MaxBlackHoles;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            Vector2 spawnPosition = player.Center + new Vector2(0f, -160f);
            Projectile minion = Projectile.NewProjectileDirect(source, spawnPosition, Vector2.Zero, type, damage, knockback, player.whoAmI);
            minion.originalDamage = Item.damage;
            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.FragmentVortex, 8)
                .AddIngredient(ItemID.FragmentNebula, 8)
                .AddIngredient(ItemID.FragmentSolar, 8)
                .AddIngredient(ItemID.FragmentStardust, 8)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
