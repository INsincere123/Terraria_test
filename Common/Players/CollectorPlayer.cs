using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 收集者饰品玩家钩子。
    ///
    /// 处决逻辑（在 OnHitNPC 里检查击中后血量，不依赖 SetInstantKill 传播链）：
    ///   每次命中 Boss 后，若 Boss 剩余血量低于 5%，立即触发处决效果：
    ///   播放音效、显示视觉冲击数字，若 Boss 仍存活则调 StrikeInstantKill()。
    ///   用 _lastExecutedWhoAmI 防止同帧多发弹幕重复触发。
    ///
    /// 奖励发放由 CollectorSystem 负责（Boss 全部死亡时统一处理）。
    /// </summary>
    public class CollectorPlayer : ModPlayer
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const float ExecuteThreshold = 0.25f; // Boss 血量低于此比例时处决（5%）
        // ─────────────────────────────────────────────────────────────

        private static readonly Microsoft.Xna.Framework.Color ExecuteColor
            = new Microsoft.Xna.Framework.Color(255, 50, 20);

        /// <summary>本帧是否装备了收集者（ResetEffects 清空，UpdateAccessory 设置）。</summary>
        public bool IsEquipped;

        // 本帧已处决过的 NPC whoAmI，防止多发弹幕在同帧重复触发处决效果
        private int _lastExecutedWhoAmI = -1;

        // ════════════════════════════════════════════════════════════════
        //   ResetEffects — 每帧清空
        // ════════════════════════════════════════════════════════════════
        public override void ResetEffects()
        {
            IsEquipped          = false;
            _lastExecutedWhoAmI = -1;
        }

        // ════════════════════════════════════════════════════════════════
        //   OnHitNPC / OnHitNPCWithProj — 检查击中后血量，触发处决
        //
        //   选择 OnHit 而非 ModifyHit 的原因：
        //     OnHit 拿到的是本次伤害已生效后的 target.life，判断更直接；
        //     无需依赖 SetInstantKill → hit.InstantKill 的传播链，更可靠。
        // ════════════════════════════════════════════════════════════════
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => TryExecute(target);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
            => TryExecute(target);

        // ── 处决核心 ─────────────────────────────────────────────────
        private void TryExecute(NPC target)
        {
            if (!IsEquipped) return;
            if (!target.boss || target.lifeMax <= 0) return;
            if (_lastExecutedWhoAmI == target.whoAmI) return; // 本帧已处理过此 Boss

            // 击中后血量比例（负值表示本次命中已将 Boss 打死）
            float lifeRatio = (float)target.life / target.lifeMax;
            if (lifeRatio >= ExecuteThreshold) return; // 仍高于 5%，不触发

            _lastExecutedWhoAmI = target.whoAmI;

            // 视觉冲击数字（无论 Boss 是否已被本次命中打死，都显示）
            CombatText.NewText(target.Hitbox, ExecuteColor, 99999999, dramatic: true);

            // 金色公告文本（通知栏）
            Main.NewText($"{target.FullName} 已被处决", new Microsoft.Xna.Framework.Color(255, 215, 0));
            // 处决音效（MoonLord = NPC_Killed_10，SoundType.Sound，MaxInstances=0，全局播放）
            SoundEngine.PlaySound(SoundID.MoonLord, null);

            // 若 Boss 被本次命中打至低于 5% 但尚未死亡（伤害不足以杀死），补一刀确保死亡
            if (target.active && target.life > 0)
                target.StrikeInstantKill();
        }
    }
}
