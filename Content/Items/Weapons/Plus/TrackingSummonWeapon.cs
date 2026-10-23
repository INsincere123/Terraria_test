using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;

namespace TestMod.Content.Items.Weapons.Plus
{
    public abstract class TrackingSummonWeapon : ModItem
    {
        public abstract TrackingWeaponKind TrackingKind { get; }
        public abstract int OriginalType { get; }
        public override string Texture => $"Terraria/Images/Item_{OriginalType}";

        public override void SetStaticDefaults()
        {
            Item.staff[Type] = Item.staff[OriginalType];
            ItemID.Sets.StaffMinionSlotsRequired[Type] = ItemID.Sets.StaffMinionSlotsRequired[OriginalType];
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = ItemID.Sets.GamepadWholeScreenUseRange[OriginalType];
            ItemID.Sets.LockOnIgnoresCollision[Type] = ItemID.Sets.LockOnIgnoresCollision[OriginalType];
            ItemID.Sets.SkipsInitialUseSound[Type] = ItemID.Sets.SkipsInitialUseSound[OriginalType];
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = ItemID.Sets.ItemsThatAllowRepeatedRightClick[OriginalType];
        }

        public override void SetDefaults() => Item.CloneDefaults(OriginalType);

        public override void UseStyle(Player player, Rectangle heldItemFrame)
        {
            // 原版花朵按物品 ID 修正举起位置，CloneDefaults 不包含此分支。
            if (TrackingKind != TrackingWeaponKind.Abigail) return;
            player.itemLocation.X -= 10f * player.direction;
            player.itemLocation.Y -= 2f * player.gravDir;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.whoAmI != Main.myPlayer || player.altFunctionUse == 2) return false;
            player.AddBuff(Item.buffType, 3600);
            // 复用原版鼠标召唤与基础伤害重算；蜘蛛按原版顺序轮换三种形态。
            if (TrackingKind == TrackingWeaponKind.Spider)
            {
                type += player.nextCycledSpiderMinionType;
                player.nextCycledSpiderMinionType = (player.nextCycledSpiderMinionType + 1) % 3;
            }
            player.SpawnMinionOnCursor(source, player.whoAmI, type, Item.damage, knockback);
            return false;
        }
    }

    public sealed class DesertTigerStaffPlus : TrackingSummonWeapon
    {
        public override TrackingWeaponKind TrackingKind => TrackingWeaponKind.DesertTiger;
        public override int OriginalType => ItemID.StormTigerStaff;

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.StormTigerStaff)
            .AddIngredient(ItemID.SpectreBar, 10)
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
    public sealed class RavenStaffPlus : TrackingSummonWeapon
    {
        public override TrackingWeaponKind TrackingKind => TrackingWeaponKind.Raven;
        public override int OriginalType => ItemID.RavenStaff;

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.RavenStaff)
            .AddIngredient(ItemID.SpectreBar, 10)
            .AddTile(TileID.MythrilAnvil)
            .Register();
    }
    public sealed class AbigailsFlowerPlus : TrackingSummonWeapon
    {
        public override TrackingWeaponKind TrackingKind => TrackingWeaponKind.Abigail;
        public override int OriginalType => ItemID.AbigailsFlower;

        public override void AddRecipes()
        {
            foreach (int bar in new[] { ItemID.GoldBar, ItemID.PlatinumBar })
                CreateRecipe()
                    .AddIngredient(ItemID.AbigailsFlower)
                    .AddIngredient(bar, 10)
                    .AddTile(TileID.Anvils)
                    .Register();
        }
    }
    public sealed class SpiderStaffPlus : TrackingSummonWeapon
    {
        public override TrackingWeaponKind TrackingKind => TrackingWeaponKind.Spider;
        public override int OriginalType => ItemID.SpiderStaff;

        public override void AddRecipes()
        {
            foreach (int bar in new[] { ItemID.AdamantiteBar, ItemID.TitaniumBar })
                CreateRecipe()
                    .AddIngredient(ItemID.SpiderStaff)
                    .AddIngredient(bar, 10)
                    .AddTile(TileID.MythrilAnvil)
                    .Register();
        }
    }
}
