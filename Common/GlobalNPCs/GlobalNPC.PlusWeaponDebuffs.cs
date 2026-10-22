using System;
using Terraria;
using Terraria.ID;
using TestMod.Common.DataStructures;
using TestMod.Common.Systems;
using ProjectileState = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC
    {
        private const int BuffID_Celled = BuffID.StardustMinionBleed;
        private const int BuffID_Daybroken = BuffID.Daybreak;

        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            ClearArmorShredIfExpired(npc);
            Electrified_UpdateLifeRegen(npc, ref damage);

            if (npc.HasBuff(BuffID_Celled) && HasAttachedPlus(npc, ProjectileID.StardustCellMinionShot, PlusWeaponKind.StardustCell))
            {
                npc.lifeRegen -= 80 * 170;
                damage = Math.Max(damage, 40 * 180);
            }

            if (npc.HasBuff(BuffID_Daybroken) && HasAttachedPlus(npc, ProjectileID.Daybreak, PlusWeaponKind.Daybreak))
            {
                npc.lifeRegen -= 200 * 9;
                damage = Math.Max(damage, 100 * 10);
            }
        }

        private static bool HasAttachedPlus(NPC npc, int type, PlusWeaponKind kind)
        {
            foreach (Projectile p in ProjectileLookup.OfType(type))
            {
                if (p.ai[0] == 1f && p.ai[1] == npc.whoAmI && p.owner >= 0 && p.owner < Main.maxPlayers &&
                    Main.player[p.owner].active && p.GetGlobalProjectile<ProjectileState>().HasPlusWeapon(kind))
                    return true;
            }

            return false;
        }
    }
}
