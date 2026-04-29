using Terraria;
using Terraria.ModLoader;

namespace 武器test.Buffs
{
    // 引力井增益 buff —— 存在期间持续对周围敌人施加拉取
    // 按键触发时施加，再次触发则刷新持续时间
    public class AntaresGravityWellBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            // 增益 buff，状态栏显示正常边框
            Main.buffNoSave[Type] = true;
        }
    }
}
