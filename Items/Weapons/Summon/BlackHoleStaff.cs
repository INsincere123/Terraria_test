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
        private const float MinionSlotCost = 9f;

        public override void SetStaticDefaults()
        {
            ItemID.Sets.StaffMinionSlotsRequired[Type] = MinionSlotCost;    // 每个黑洞需要 9 格仆从位
        }

        public override void SetDefaults()
        {
            Item.width = 42;
            Item.height = 42;
            Item.damage = 2950;
            Item.mana = 0;
            Item.useAnimation = 24;
            Item.useTime = 24;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.knockBack = 88.6f;
            Item.value = Item.sellPrice(platinum: 10);
            Item.rare = ModContent.RarityType<EventHorizonRarity>();
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.Summon;
            Item.buffType = ModContent.BuffType<BlackHoleMinionBuff>();
            Item.shoot = ModContent.ProjectileType<BlackHoleMinion>();
        }

        public override bool CanUseItem(Player player)
        {
            if (player.ownedProjectileCounts[Item.shoot] >= MaxBlackHoles)
                return false;

            if (player.maxMinions < MinionSlotCost)
                return false;

            float usedSlots = GetUsedMinionSlots(player);
            return usedSlots + MinionSlotCost <= player.maxMinions + 0.01f;
        }

        private static float GetUsedMinionSlots(Player player)
        {
            float usedSlots = 0f;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];
                if (projectile.active && projectile.owner == player.whoAmI && projectile.minion)
                    usedSlots += projectile.minionSlots;
            }

            return usedSlots;
        }

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
                .AddIngredient(ItemID.FragmentVortex, 999)
                .AddIngredient(ItemID.FragmentNebula, 999)
                .AddIngredient(ItemID.FragmentSolar, 999)
                .AddIngredient(ItemID.FragmentStardust, 999)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
