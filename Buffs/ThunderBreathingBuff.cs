using Terraria;
using Terraria.ModLoader;
using 武器test.Common.Players;
using 武器test.Projectiles.Minions;

namespace 武器test.Buffs
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
