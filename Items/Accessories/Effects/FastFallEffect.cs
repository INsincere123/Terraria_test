using Terraria;

namespace TestMod.Items.Accessories.Effects
{
    // ============================================================================
    //  FastFallEffect  ——  快速下落 (InterstellarStompers 风格)
    // ----------------------------------------------------------------------------
    //  按下"下"键 (非飞行状态) 时:
    //    - 提高最大下落速度 (vanilla 默认 maxFallSpeed = 10f)
    //    - 提高重力 (加速达到上限)
    //    - 完全穿透平台 (不会先踩上去再穿过)
    //
    //  使用示例:
    //
    //    FastFallEffect.Apply(player, maxFallSpeed: 30f, gravityBoost: 2f);
    //
    //  注意:
    //   - maxFallSpeed 和 gravity 必须在 PostUpdateRunSpeeds 钩子改 (vanilla 在此之后才检查上限)
    //   - 实际钩子由 OmniEffectsPlayer.PostUpdateRunSpeeds 分发到 UpdateRunSpeeds()
    // ============================================================================

    public static class FastFallEffect
    {
        public static void Apply(Player player, float maxFallSpeed, float gravityBoost)
        {
            var mp = player.GetModPlayer<OmniEffectsPlayer>();
            mp.EnableFastFall          = true;
            mp.FastFall_MaxFallSpeed   = maxFallSpeed;
            mp.FastFall_GravityBoost   = gravityBoost;
        }

        // =====================================================================
        // 由 OmniEffectsPlayer.PostUpdateRunSpeeds 调用
        // =====================================================================
        public static void UpdateRunSpeeds(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnableFastFall) return;

            // 按下键 + 正在下落 + 没有按跳跃键 (飞行时用翅膀悬停逻辑, 不干扰)
            if (player.controlDown && !player.controlJump && player.velocity.Y > 0f)
            {
                player.maxFallSpeed = mp.FastFall_MaxFallSpeed;
                player.gravity     *= mp.FastFall_GravityBoost;
                player.GoingDownWithGrapple = true; // 完全无视平台
            }
        }
    }
}
