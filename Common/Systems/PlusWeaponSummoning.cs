using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using TestMod.Common.DataStructures;
using ProjectileState = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Common.Systems
{
    internal static class PlusWeaponSummoning
    {
        // 只在实际开始使用后切换，查询可用性、缺魔力及右键指定目标不应销毁已有龙。
        internal static void PrepareDragon(Player player, bool empowered)
        {
            if (player.whoAmI != Main.myPlayer) return;
            bool replace = false;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && ProjectileID.Sets.StardustDragon[p.type])
                    replace |= p.GetGlobalProjectile<ProjectileState>().HasPlusWeapon(PlusWeaponKind.StardustDragon) != empowered;
            }
            if (!replace) return;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && ProjectileID.Sets.StardustDragon[p.type]) p.Kill();
            }
        }

        internal static void SpawnDragon(Player player, IEntitySource source, Item item, Vector2 position,
            int type, int damage, float knockback)
        {
            int head = -1, tail = -1;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI) continue;
                if (p.type == ProjectileID.StardustDragon1) head = i;
                if (p.type == ProjectileID.StardustDragon4) tail = i;
            }
            // 全部本次新段使用物品来源，保留原版 ai / localAI 链与未缩放基础伤害。
            int Spawn(int projectileType, int previous = 0)
            {
                int index = Projectile.NewProjectile(source, position, Vector2.Zero, projectileType,
                    damage, knockback, player.whoAmI, previous);
                if (index < Main.maxProjectiles)
                    Main.projectile[index].originalDamage = (int)(item.damage * PlusWeaponDefinition.For(PlusWeaponKind.StardustDragon).WeaponDamage);
                return index;
            }
            if (head == -1 && tail == -1)
            {
                position = Main.MouseWorld;
                player.LimitPointToPlayerReachableArea(ref position);
                // 提前检查空位，避免部分生成后留下断链。
                if (FreeSlots() < 4) return;
                int first = Spawn(type), second = Spawn(type + 1, first);
                int third = Spawn(type + 2, second), last = Spawn(type + 3, third);
                Main.projectile[second].localAI[1] = third;
                Main.projectile[third].localAI[1] = last;
                Main.projectile[second].netUpdate = Main.projectile[third].netUpdate = true;
            }
            else if (head != -1 && tail != -1 && FreeSlots() >= 2)
            {
                int previous = (int)Main.projectile[tail].ai[0];
                int second = Spawn(type + 1, previous), third = Spawn(type + 2, second);
                Main.projectile[second].localAI[1] = third;
                Main.projectile[third].localAI[1] = tail;
                Main.projectile[tail].ai[0] = third;
                Main.projectile[tail].originalDamage = (int)(item.damage * PlusWeaponDefinition.For(PlusWeaponKind.StardustDragon).WeaponDamage);
                foreach (int index in new[] { second, third, tail })
                {
                    Main.projectile[index].ai[1] = 1f;
                    Main.projectile[index].netUpdate = true;
                }
            }
        }

        private static int FreeSlots()
        {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) if (!Main.projectile[i].active) count++;
            return count;
        }
    }
}
