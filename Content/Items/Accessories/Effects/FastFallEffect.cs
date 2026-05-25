using Terraria;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  FastFallEffect  ——  快速下落 (InterstellarStompers 风格)
    // ----------------------------------------------------------------------------
    //  按下"下"键 (非飞行状态、正在下落) 时加速下落，完全穿透平台。
    //
    //  ── 灾厄兼容 ──────────────────────────────────────────────────────────────
    //  灾厄加载时：
    //    每帧通过反射设 CalamityPlayer.gSabaton = true，
    //    灾厄在其 PostUpdateRunSpeeds 里执行：
    //      maxFallSpeed *= 2  +  上升时 velocity.Y *= 0.7（快速转向下落）
    //    这给出与 InterstellarStompers 完全一致的加速手感。
    //    我们在灾厄之后的 PostUpdateRunSpeeds 里再把上限拉到 FastFall_MaxFallSpeed，
    //    防止灾厄的 *=2 不断翻倍。
    //
    //  未加载灾厄时：
    //    在 PostUpdateRunSpeeds 里直接设置 maxFallSpeed 和重力加速度。
    //
    //  使用示例:
    //    FastFallEffect.Apply(player, maxFallSpeed: 30f, gravityBoost: 2f);
    // ============================================================================

    public static class FastFallEffect
    {
        public static void Apply(Player player, float maxFallSpeed, float gravityBoost)
        {
            var mp = player.GetModPlayer<OmniEffectsPlayer>();
            mp.EnableFastFall        = true;
            mp.FastFall_MaxFallSpeed = maxFallSpeed;
            mp.FastFall_GravityBoost = gravityBoost;

            // 灾厄加载时每帧设 gSabaton，让灾厄自己的加速逻辑跑。
            // GoingDownWithGrapple 不在这里设，必须在 UpdateRunSpeeds 里按条件设。
            if (CalamityCompatSystem.CalamityLoaded)
                CalamityCompatSystem.ActivateFastFall(player);
        }

        // =====================================================================
        // 由 OmniEffectsPlayer.PostUpdateRunSpeeds 调用
        // =====================================================================
        public static void UpdateRunSpeeds(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnableFastFall) return;

            // 触发条件：按下键 + 正在下落 + 不按跳跃
            bool fastFalling = player.controlDown
                            && !player.controlJump
                            && player.velocity.Y * player.gravDir > 0f;

            if (!fastFalling) return;

            // 穿透平台（仅在主动快速下落时才设，防止走路状态踩不了平台）
            player.GoingDownWithGrapple = true;

            if (CalamityCompatSystem.CalamityLoaded)
            {
                // 灾厄模式：gSabaton 已激活，灾厄负责加速逻辑（maxFallSpeed *= 2）。
                // 我们在灾厄之后把上限钳到 FastFall_MaxFallSpeed，防止每帧翻倍失控。
                player.maxFallSpeed = mp.FastFall_MaxFallSpeed;
            }
            else
            {
                // 无灾厄：自己处理加速
                player.maxFallSpeed = mp.FastFall_MaxFallSpeed;
                player.gravity     *= mp.FastFall_GravityBoost;
            }
        }
    }
}
