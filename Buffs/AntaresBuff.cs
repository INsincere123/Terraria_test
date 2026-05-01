using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Projectiles.Minions;

namespace TestMod.Buffs
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