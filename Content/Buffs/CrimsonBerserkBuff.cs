using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    // 猩红暴走状态 —— 由 BloodFeedPlayer 管理时长，此 Buff 仅作视觉标识
    // 不可手动移除
    public class CrimsonBerserkBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type]     = false;
            Main.buffNoSave[Type] = true;
            // 正常显示倒计时，时长由 AddBuff 传入的实际帧数决定
        }

        public override bool RightClick(int buffIndex) => false; // 禁止手动移除
    }
}
