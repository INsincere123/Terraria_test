using Terraria;
using Terraria.ModLoader;
using TestMod.Buffs;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 超能护盾专属玩家状态。
    /// 负责追踪紧急护盾的触发、衰减和冷却。
    ///
    /// 紧急护盾触发条件：
    ///   受到伤害后本帧生命值跌破最大生命的 30%，且冷却已结束
    ///
    /// 触发后行为：
    ///   ·立即为护盾血池追加 (50% 最大生命 + 100% 防御) 点护盾
    ///   ·该护盾组件在 4.5 秒内线性衰减至 0（通过 GetMaxShield 动态计算实现）
    ///   ·触发 120 秒冷却，以 Buff 图标显示倒计时
    /// </summary>
    public class SuperEnergyShieldPlayer : ModPlayer
    {
        // ── 常量 ──────────────────────────────────────────────────────
        public const int EmergencyFrames = (int)(4.5f * 60); // 270 帧 = 4.5 秒衰减
        public const int CooldownFrames  = 120 * 60;         // 7200 帧 = 120 秒冷却

        // ── 状态 ──────────────────────────────────────────────────────
        /// <summary>紧急护盾触发时的最大值（触发瞬间固定，随后由 timer 决定实际比例）。</summary>
        public float EmergencyMaxValue { get; private set; }

        /// <summary>紧急护盾剩余帧数（> 0 表示激活中）。</summary>
        public int EmergencyTimer { get; private set; }

        /// <summary>紧急护盾是否处于激活状态。</summary>
        public bool EmergencyActive => EmergencyTimer > 0;

        /// <summary>冷却剩余帧数。</summary>
        public int Cooldown { get; private set; }

        /// <summary>本帧是否装备了超能护盾（由 UpdateAccessory 设置，ResetEffects 清空）。</summary>
        internal bool IsEquipped;

        // 上一帧生命值比例（用于检测跌破 30%）
        private float _prevLifeFrac = 1f;

        // ═══════════════════════════════════════════════════════════════
        //   ResetEffects — 每帧清空装备标记
        // ═══════════════════════════════════════════════════════════════
        public override void ResetEffects()
        {
            IsEquipped = false;
        }

        // ═══════════════════════════════════════════════════════════════
        //   PostUpdate — 计时、触发检测、Buff 刷新
        // ═══════════════════════════════════════════════════════════════
        public override void PostUpdate()
        {
            if (!IsEquipped)
            {
                // 未装备时停止计时，防止状态残留
                _prevLifeFrac = (float)Player.statLife / System.Math.Max(1, Player.statLifeMax2);
                return;
            }

            // 冷却倒计时 + Buff 图标显示
            if (Cooldown > 0)
            {
                Cooldown--;
                Player.AddBuff(ModContent.BuffType<SuperEnergyShieldCooldownBuff>(), Cooldown + 1);
            }

            // 紧急护盾衰减计时
            if (EmergencyTimer > 0)
                EmergencyTimer--;

            // 触发检测：本帧生命值首次跌破 30% 且冷却已结束
            float lifeFrac = (float)Player.statLife / System.Math.Max(1, Player.statLifeMax2);
            if (lifeFrac < 0.30f && _prevLifeFrac >= 0.30f && Cooldown <= 0)
                TriggerEmergency();

            _prevLifeFrac = lifeFrac;
        }

        // ── 紧急护盾触发 ─────────────────────────────────────────────
        private void TriggerEmergency()
        {
            float value = Player.statLifeMax2 * 0.5f + Player.statDefense * 1.0f;
            EmergencyMaxValue = value;
            EmergencyTimer    = EmergencyFrames;
            Cooldown          = CooldownFrames;

            // 直接追加护盾量；ShieldPlayer 下一帧 PostUpdateMiscEffects 会同步上限
            Player.GetModPlayer<ShieldPlayer>().AddShield(value);
        }
    }
}
