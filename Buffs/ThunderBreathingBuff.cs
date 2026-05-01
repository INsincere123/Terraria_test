using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Projectiles.Minions;

namespace TestMod.Buffs
{
    public class ThunderBreathingBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            var modPlayer = player.GetModPlayer<ThunderBreathingMinionPlayer>();

            if (player.ownedProjectileCounts[ModContent.ProjectileType<ZenitsuMinion>()] > 0)
            {
                modPlayer.thunderBreathing = true;
            }

            if (!modPlayer.thunderBreathing)
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}
