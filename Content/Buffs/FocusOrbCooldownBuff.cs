using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    /// <summary>
    /// 专注宝珠冷却 Buff。仅用于显示主动技能的剩余冷却时间，无其他效果。
    /// </summary>
    public class FocusOrbCooldownBuff : ModBuff
    {
        // 复用原版 Buff 94 贴图作为冷却图标
        public override string Texture => "Terraria/Images/Buff_94";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type]    = true;  // 标记为 debuff，防止玩家右键点掉
            Main.buffNoSave[Type] = true;
        }
    }
}
