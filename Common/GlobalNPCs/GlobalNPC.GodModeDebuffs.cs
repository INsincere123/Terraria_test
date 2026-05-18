using System;
using Terraria;
using Terraria.ID;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC
    {
        private const int BuffID_Celled = BuffID.StardustMinionBleed;
        private const int BuffID_Daybroken = BuffID.Daybreak;

        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            ClearArmorShredIfExpired(npc);

            if (!AnyPlayerInGodMode())
                return;

            if (npc.HasBuff(BuffID_Celled))
            {
                npc.lifeRegen -= 80 * 170;
                damage = Math.Max(damage, 40 * 180);
            }

            if (npc.HasBuff(BuffID_Daybroken))
            {
                npc.lifeRegen -= 200 * 9;
                damage = Math.Max(damage, 100 * 10);
            }
        }

        private static bool AnyPlayerInGodMode()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player.active && player.GetModPlayer<GodModePlayer>().GodModeBuff)
                    return true;
            }

            return false;
        }
    }
}
