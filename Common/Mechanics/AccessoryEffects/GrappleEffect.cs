using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    /// <summary>
    /// 红木魔石钩爪效果：
    ///   - 所有钩爪拉人速度 ×1.5，飞出阶段额外更新 +1（普通钩爪约 ×2），缩回速度 ×3
    ///   - 被钩爪拉动时获得 10% 伤害减免 + 50% 荆棘效果
    ///   - 伤害减免在钩爪收回后持续 1 秒（60帧）
    /// </summary>
    public static class GrappleEffect
    {
        // ── 在 UpdateAccessory 中调用 ──────────────────────────────────
        public static void Apply(Player player, Item source)
        {
            if (FargoAccessoryCompatSystem.TryProvide(FargoAccessoryCompatSystem.Mahogany, player, source)) return;
            player.GetModPlayer<OmniEffectsPlayer>().EnableGrapple = true;
        }

        // ── 由 OmniEffectsPlayer.PostUpdateMiscEffects 每帧调用 ────────
        public static void UpdateMiscEffects(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnableGrapple)
            {
                ClearDamageReduction(mp);
                return;
            }

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

        // 卸装、死亡和进入世界时清理，避免重新装备恢复旧的减伤窗口。
        internal static void ClearDamageReduction(OmniEffectsPlayer mp)
        {
            mp.GrappleDRActive = false;
            mp.GrappleDRTimer = 0;
        }

        // PreAI/PostAI 共用：只管理本效果的 +1，AI 中附着/收回后立即撤销。
        internal static void UpdateLaunchSpeed(Projectile projectile, ref bool applied, bool allowBoost)
        {
            bool enabled = false;
            if (allowBoost && projectile.active && projectile.aiStyle == ProjAIStyleID.Hook &&
                projectile.ai[0] == 0f && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[projectile.owner];
                if (owner != null && owner.active && !owner.dead)
                    // owner 决定装备注入；远端保留同步的贡献，仍能在阶段结束/死亡时清理。
                    enabled = projectile.owner != Main.myPlayer ? applied :
                        owner.TryGetModPlayer(out OmniEffectsPlayer state) && state.EnableGrapple;
            }
            if (enabled == applied) return;
            SetLaunchBoost(projectile, ref applied, enabled);
            if (projectile.owner == Main.myPlayer) projectile.netUpdate = true;
        }

        // 同步只改变自身贡献，重复包不叠加；不能清零其他来源或钩爪自身的更新次数。
        internal static void SetLaunchBoost(Projectile projectile, ref bool applied, bool enabled)
        {
            if (enabled == applied) return;
            projectile.extraUpdates = System.Math.Max(0, projectile.extraUpdates + (enabled ? 1 : -1));
            applied = enabled;
        }
    }
}
