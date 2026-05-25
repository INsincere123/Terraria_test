using Terraria;
using TestMod.Content.Items.Accessories.Dashes;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  CustomDashEffect  ——  自定义 dash 接入辅助
    // ----------------------------------------------------------------------------
    //  一行调用接入项目的 PlayerDashManager 系统 (见 Dashes/ 文件夹)
    //
    //  灾厄 (Calamity) 风格做法:
    //    [1] 设置 LongDashEffectId = 自定义 dash 名字
    //    [2] 同时把 vanilla dashType 置 0, 禁用 vanilla 双击 dash, 避免冲突
    //
    //  使用示例:
    //
    //    CustomDashEffect.Apply(player, "LongDash");
    //
    //  传入的 dashId 必须是 PlayerDashManager 中已注册的 dash 名字。
    // ============================================================================

    public static class CustomDashEffect
    {
        public static void Apply(Player player, string dashId)
        {
            if (string.IsNullOrEmpty(dashId)) return;

            player.GetModPlayer<DashPlayer>().LongDashEffectId = dashId;
            player.dashType = 0;
        }
    }
}
