using Terraria;
using Terraria.ModLoader;
using 武器test.Common.Players;
using 武器test.Projectiles.Minions;

namespace 武器test.Buffs
{
    public class SiriusBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            var modPlayer = player.GetModPlayer<SiriusMinionPlayer>();

            if (player.ownedProjectileCounts[ModContent.ProjectileType<SiriusMinion>()] > 0)
            {
                modPlayer.sirius = true;
            }

            if (!modPlayer.sirius)
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}