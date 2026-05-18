using Terraria;
using Terraria.ModLoader;

namespace TestMod.Items.Accessories.Effects
{
    /// <summary>
    /// 红木魔石钩爪效果：
    ///   - 所有钩爪拉人速度 ×1.5，飞出速度 ×2（extraUpdates），缩回速度 ×3
    ///   - 被钩爪拉动时获得 10% 伤害减免 + 50% 荆棘效果
    ///   - 伤害减免在钩爪收回后持续 1 秒（60帧）
    /// </summary>
    public static class GrappleEffect
    {
        // ── 在 UpdateAccessory 中调用 ──────────────────────────────────
        public static void Apply(Player player)
        {
            player.GetModPlayer<OmniEffectsPlayer>().EnableGrapple = true;
        }

        // ── 由 OmniEffectsPlayer.PostUpdateMiscEffects 每帧调用 ────────
        public static void UpdateMiscEffects(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnableGrapple) return;

            if (player.grapCount > 0)
            {
                // 被拉动时：荆棘 + 激活伤害减免，重置倒计时
                player.thorns     += 0.5f;
                mp.GrappleDRActive = true;
                mp.GrappleDRTimer  = 60;
            }
            else if (mp.GrappleDRActive)
            {
                // 松钩后：倒计时 1 秒
                if (--mp.GrappleDRTimer <= 0)
                    mp.GrappleDRActive = false;
            }

            if (mp.GrappleDRActive)
                player.endurance += 0.1f; // 10% 伤害减免
        }

        // ── 由 GlobalProjectile.PreAI 调用：飞行阶段速度 ×2 ────────
        // 利用 extraUpdates=1 让钩爪弹射物每帧执行两次 AI，等效于飞出速度翻倍。
        // 仅在飞行阶段（ai[0] == 0）生效，附着后自动恢复原节奏。
        public static void TryBoostLaunchSpeed(Projectile projectile)
        {
            if (projectile.aiStyle != 7) return;           // 非钩爪 AI
            if (projectile.ai[0] != 0f)  return;           // 非飞行阶段

            Player owner = Main.player[projectile.owner];
            if (owner == null || !owner.active) return;

            if (owner.GetModPlayer<OmniEffectsPlayer>().EnableGrapple)
                projectile.extraUpdates = 1;               // ×2 飞出速度
        }
    }
}
