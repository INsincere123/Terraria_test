using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    // 仅保留旧内容身份；诱敌已改为独立的稻草人道具，本 Buff 不再提供效果。
    public class BlindnessDebuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_18"; // 借用黑暗 debuff 图标

        public override void SetStaticDefaults()
        {
            Main.debuff[Type]     = true;
            Main.pvpBuff[Type]    = true;
            Main.buffNoSave[Type] = true;
        }
    }
}
