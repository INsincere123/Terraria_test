using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    /// <summary>
    /// 黄泉 — 玩家处于黄泉馈灵塔范围内时每帧刷新。
    /// 纯标记 buff，无任何效果；各功能模块以此判断玩家是否在范围内。
    /// </summary>
    public class HuangQuanBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type]        = true; // 重进存档不保留
            Main.buffNoTimeDisplay[Type] = true; // 每帧刷新，不显示倒计时
        }
    }
}
