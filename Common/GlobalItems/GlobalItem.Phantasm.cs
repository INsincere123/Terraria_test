using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Projectiles.Ranged;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (item.type != ItemID.Phantasm || !IsGodMode(player))
                return true;

            if (player.ownedProjectileCounts[ModContent.ProjectileType<PhantasmHoldout>()] <= 0)
            {
                Projectile.NewProjectile(source, position, velocity,
                    ModContent.ProjectileType<PhantasmHoldout>(),
                    damage, knockback, player.whoAmI);
            }

            return false;
        }
    }
}
