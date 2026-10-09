using System;
using Terraria;

namespace TestMod.Common.Mechanics.Dashes
{
    internal static class DashImmunity
    {
        // 冲刺只补足缺少的帧，不缩短受伤/闪避等来源的免疫。
        internal static void Grant(Player player, int frames)
        {
            if (frames <= 0) return;
            bool granted = !player.immune || player.immuneTime < frames;
            player.immuneTime = Math.Max(player.immuneTime, frames);
            for (int i = 0; i < player.hurtCooldowns.Length; i++)
            {
                granted |= player.hurtCooldowns[i] < frames;
                player.hurtCooldowns[i] = Math.Max(player.hurtCooldowns[i], frames);
            }
            player.immune = true;
            if (granted) player.immuneNoBlink = true;
        }
    }
}
