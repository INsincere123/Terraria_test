using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    [Autoload(true)]
    public class DarkMarkBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.debuff[Type] = true;
        }
    }
}