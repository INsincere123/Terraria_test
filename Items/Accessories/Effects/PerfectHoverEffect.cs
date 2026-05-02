using Terraria;

namespace TestMod.Items.Accessories.Effects
{
    // ============================================================================
    //  PerfectHoverEffect  ——  完美悬浮 (按下键 + 跳跃键时视觉完全静止)
    // ----------------------------------------------------------------------------
    //  原理:
    //   - 在 PreUpdateMovement (所有 velocity 计算结束后) 强制 velocity.Y 为极小负值
    //   - 关键: 不能设为 0!
    //       * vanilla 检测 velocity.Y == 0f 会判定为"在地面" -> 触发跑步动画 + 翅膀停扇 (虚空跑步 bug)
    //       * 用 -0.0001f 极小负值: 视觉上完全静止, 但 vanilla 仍认为在飞行
    //   - 仅修改 Y 分量, 水平 (X) 完全不受干扰
    //
    //  使用条件: player.wingsLogic > 0 (有翅膀逻辑生效) + 按住下键 + 按住跳跃键
    //
    //  使用示例:
    //
    //    PerfectHoverEffect.Apply(player);
    //
    //  通常和翅膀一起用, 但理论上对任何提供 wingsLogic 的来源都生效。
    // ============================================================================

    public static class PerfectHoverEffect
    {
        public static void Apply(Player player)
        {
            player.GetModPlayer<OmniEffectsPlayer>().EnablePerfectHover = true;
        }

        // =====================================================================
        // 由 OmniEffectsPlayer.PreUpdateMovement 调用
        // =====================================================================
        public static void UpdateMovement(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnablePerfectHover) return;

            if (player.wingsLogic > 0 && player.controlDown && player.controlJump)
            {
                player.velocity.Y = -0.0001f;          // 极微小向上速度: 维持飞行状态, 视觉静止
                player.gfxOffY    = 0f;                // 防止视觉抖动
                player.fallStart  = (int)(player.position.Y / 16f); // 防止跌落伤害
            }
        }
    }
}
