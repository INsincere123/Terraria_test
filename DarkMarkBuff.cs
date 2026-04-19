using Terraria;
using Terraria.ModLoader;

namespace 武器test
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