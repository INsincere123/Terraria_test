using Terraria;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC
    {
        private static bool TimeStop_ShouldFreeze(NPC npc)
        {
            if (npc.townNPC || npc.friendly)
                return false;

            if (!Main.LocalPlayer.active)
                return false;

            TimeStopPlayer modPlayer = Main.LocalPlayer.GetModPlayer<TimeStopPlayer>();
            return modPlayer.TimeStopActive;
        }

        private static void TimeStop_FreezeNPC(NPC npc)
        {
            npc.position = npc.oldPosition;
            npc.frameCounter = 0;
            npc.velocity = npc.oldVelocity;
        }

        private static void TimeStop_PostAI(NPC npc)
        {
            if (npc.townNPC || npc.friendly)
                return;
            if (!Main.LocalPlayer.active)
                return;

            TimeStopPlayer modPlayer = Main.LocalPlayer.GetModPlayer<TimeStopPlayer>();
            if (modPlayer.TimeSlowActive)
                npc.velocity *= modPlayer.TimeSlowFactor;
        }
    }
}
