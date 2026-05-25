using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    // 战后虚弱 —— 暴走结束后自动附加，由 BloodFeedPlayer 管理时长
    // 是 debuff，不可手动移除
    public class BloodExhaustionBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type]     = true;
            Main.buffNoSave[Type] = true;
            // 正常显示倒计时
        }

        public override bool RightClick(int buffIndex) => false; // 禁止手动移除
    }
}
