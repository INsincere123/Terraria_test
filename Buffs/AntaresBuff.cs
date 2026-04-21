using Terraria;
using Terraria.ModLoader;
using 武器test.Common.Players;
using 武器test.Projectiles.Minions;

namespace 武器test.Buffs
{
    public class AntaresBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            var modPlayer = player.GetModPlayer<AntaresMinionPlayer>();

            if (player.ownedProjectileCounts[ModContent.ProjectileType<AntaresMinion>()] > 0)
            {
                modPlayer.antares = true;
            }

            if (!modPlayer.antares)
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}