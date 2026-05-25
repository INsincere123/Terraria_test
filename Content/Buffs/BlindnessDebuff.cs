using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    // 纯标记型 debuff，无逻辑。
    // 实际失明行为由 GlobalNPC / GlobalProjectile 驱动。
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
