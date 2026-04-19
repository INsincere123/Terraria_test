using Terraria;
using Terraria.ModLoader;

namespace 武器test
{
    [Autoload(true)]
    public class HarvestTimeBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.debuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // 🌑 召唤攻速
            player.GetAttackSpeed(DamageClass.SummonMeleeSpeed) += 0.5f;
        }
    }
}