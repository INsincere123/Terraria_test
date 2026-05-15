using Terraria;
using Terraria.ModLoader;

namespace TestMod.Buffs
{
    /// <summary>
    /// 聚灵·冷却 Buff。
    /// 仅用于显示主动技能的剩余冷却时间，无其他效果。
    /// </summary>
    public class JuLingCooldownBuff : ModBuff
    {
        // 复用原版 Buff 21（药水病）贴图，沙漏样式适合冷却计时
        public override string Texture => "Terraria/Images/Buff_21";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type]  = true;  // 计为 debuff，防止玩家主动点掉
            Main.buffNoSave[Type] = true;
        }
    }
}
