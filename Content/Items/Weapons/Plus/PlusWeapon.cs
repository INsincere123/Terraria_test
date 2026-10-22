using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Systems;
using TestMod.Content.Projectiles.Ranged;

namespace TestMod.Content.Items.Weapons.Plus
{
    public abstract class PlusWeapon : ModItem
    {
        public abstract PlusWeaponKind Kind { get; }
        public PlusWeaponDefinition Definition => PlusWeaponDefinition.For(Kind);
        public override string Texture => $"Terraria/Images/Item_{Definition.OriginalType}";

        public override void SetStaticDefaults()
        {
            int original = Definition.OriginalType;
            Item.staff[Type] = Item.staff[original];
            ItemID.Sets.StaffMinionSlotsRequired[Type] = ItemID.Sets.StaffMinionSlotsRequired[original];
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = ItemID.Sets.GamepadWholeScreenUseRange[original];
            ItemID.Sets.LockOnIgnoresCollision[Type] = ItemID.Sets.LockOnIgnoresCollision[original];
            ItemID.Sets.SkipsInitialUseSound[Type] = ItemID.Sets.SkipsInitialUseSound[original];
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = ItemID.Sets.ItemsThatAllowRepeatedRightClick[original];
        }

        public override void SetDefaults()
        {
            Item.CloneDefaults(Definition.OriginalType);
            Item.rare = CalamityCompatSystem.CalamityRarity >= 0
                ? CalamityCompatSystem.CalamityRarity : ItemRarityID.Purple;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => damage *= Definition.WeaponDamage;
        public override void ModifyWeaponKnockback(Player player, ref StatModifier knockback) => knockback *= Definition.Knockback;
        public override bool MeleePrefix() => Kind == PlusWeaponKind.Kaleidoscope || base.MeleePrefix();

        public override bool CanConsumeAmmo(Item ammo, Player player)
            => Kind != PlusWeaponKind.Phantasm || Main.rand.NextBool(3);

        public override void HoldItem(Player player)
        {
            // 原版按物品 type 发出的手部烈焰不会由 CloneDefaults 自动继承。
            if (Kind != PlusWeaponKind.NebulaBlaze || Main.dedServ || player.HandPosition is not Vector2 hand) return;
            for (int i = 0; i < 4; i++)
            {
                Dust dust = Dust.NewDustDirect(player.Center, 0, 0, DustID.PinkTorch, player.direction * 2, 0f, 150, default, 1.3f);
                dust.position = hand - player.velocity;
                dust.noGravity = true;
                dust.fadeIn = 1f;
                dust.velocity = player.velocity;
                if (Main.rand.NextBool())
                {
                    dust.position += Main.rand.NextVector2Square(-4f, 4f);
                    dust.scale += Main.rand.NextFloat();
                    if (Main.rand.NextBool()) dust.customData = player;
                }
            }
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity,
            ref int type, ref int damage, ref float knockback)
        {
            if (Kind == PlusWeaponKind.Daybreak) position.Y = player.position.Y + player.height / 3f;
            if (Kind != PlusWeaponKind.NebulaBlaze) return;
            // 原版星云烈焰：20% 强化弹、三倍伤害，其余初速减一。
            if (Main.rand.Next(100) < 20) { type = ProjectileID.NebulaBlaze2; damage *= 3; }
            else velocity = velocity.SafeNormalize(Vector2.UnitX * player.direction) * (velocity.Length() - 1f);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.whoAmI != Main.myPlayer) return false;
            // 原版的仆从 Buff 通过物品 ID 分支维持，新注册类型须自行添加。
            if (Kind is PlusWeaponKind.StardustDragon or PlusWeaponKind.StardustCell or PlusWeaponKind.Terraprisma)
                player.AddBuff(Item.buffType, 3600);
            if (Kind == PlusWeaponKind.Phantasm)
            {
                if (player.ownedProjectileCounts[ModContent.ProjectileType<PhantasmHoldout>()] == 0)
                    Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<PhantasmHoldout>(),
                        damage, knockback, player.whoAmI);
                return false;
            }
            if (Kind == PlusWeaponKind.StardustDragon)
            {
                PlusWeaponSummoning.SpawnDragon(player, source, Item, position, type, damage, knockback);
                return false;
            }
            if (Kind is PlusWeaponKind.StardustCell or PlusWeaponKind.Terraprisma)
            {
                // SpawnMinionOnCursor 以基础伤害生成，随后由玩家属性持续重算。
                int index = player.SpawnMinionOnCursor(source, player.whoAmI, type,
                    (int)(Item.damage * Definition.WeaponDamage), knockback);
                if (index >= 0 && index < Main.maxProjectiles && Kind == PlusWeaponKind.StardustCell)
                    Main.projectile[index].ai[1] = 1f;
                return false;
            }
            if (Kind is PlusWeaponKind.LunarPortal or PlusWeaponKind.RainbowCrystal)
            {
                int y = (int)Main.MouseWorld.Y / 16;
                if (player.gravDir == -1f)
                    y = (int)(Main.screenPosition.Y + Main.screenHeight - Main.mouseY) / 16;
                int index = Projectile.NewProjectile(source, new Vector2(Main.MouseWorld.X, y * 16 - 24),
                    new Vector2(0f, 15f), type, damage, knockback, player.whoAmI);
                if (index >= 0 && index < Main.maxProjectiles)
                    Main.projectile[index].originalDamage = (int)(Item.damage * Definition.WeaponDamage);
                player.UpdateMaxTurrets();
                return false;
            }
            if (Kind == PlusWeaponKind.NebulaBlaze)
            {
                float angle = 0f;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    angle = (Main.rand.NextFloat() - 0.5f) * MathHelper.PiOver4 * 0.7f;
                    if (Collision.CanHit(position, 0, 0, position + velocity.RotatedBy(angle) * 100f, 0, 0)) break;
                }
                velocity = velocity.RotatedBy(angle) * Main.rand.NextFloat(0.95f, 1.25f);
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                return false;
            }
            return true;
        }

        public override void AddRecipes() => CreateRecipe().AddIngredient(Definition.OriginalType)
            .AddIngredient(ItemID.LunarBar, 10).AddTile(TileID.LunarCraftingStation).Register();
    }

    public sealed class PhantasmPlus : PlusWeapon { public override PlusWeaponKind Kind => PlusWeaponKind.Phantasm; }
    public sealed class DaybreakPlus : PlusWeapon { public override PlusWeaponKind Kind => PlusWeaponKind.Daybreak; }
    public sealed class NebulaBlazePlus : PlusWeapon { public override PlusWeaponKind Kind => PlusWeaponKind.NebulaBlaze; }
    public sealed class StardustDragonStaffPlus : PlusWeapon { public override PlusWeaponKind Kind => PlusWeaponKind.StardustDragon; }
    public sealed class StardustCellStaffPlus : PlusWeapon { public override PlusWeaponKind Kind => PlusWeaponKind.StardustCell; }
    public sealed class LunarPortalStaffPlus : PlusWeapon { public override PlusWeaponKind Kind => PlusWeaponKind.LunarPortal; }
    public sealed class RainbowCrystalStaffPlus : PlusWeapon { public override PlusWeaponKind Kind => PlusWeaponKind.RainbowCrystal; }
    public sealed class TerraprismaPlus : PlusWeapon { public override PlusWeaponKind Kind => PlusWeaponKind.Terraprisma; }
    public sealed class KaleidoscopePlus : PlusWeapon { public override PlusWeaponKind Kind => PlusWeaponKind.Kaleidoscope; }
}
