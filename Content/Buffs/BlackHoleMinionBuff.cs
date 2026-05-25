using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Content.Projectiles.Minions;

namespace TestMod.Content.Buffs
{
    public class BlackHoleMinionBuff : ModBuff
    {

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            var modPlayer = player.GetModPlayer<BlackHoleMinionPlayer>();

            if (player.ownedProjectileCounts[ModContent.ProjectileType<BlackHoleMinion>()] > 0)
                modPlayer.blackHoleMinion = true;

            if (!modPlayer.blackHoleMinion)
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}
