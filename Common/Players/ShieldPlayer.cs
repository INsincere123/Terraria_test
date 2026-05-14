using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.DataStructures;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 护盾系统核心 ModPlayer。
    ///
    /// ── 每帧调用顺序（tModLoader 保证）────────────────────────────────────────────
    ///   1. ResetEffects()          → 清空 _defs，重置汇总数据
    ///   2. 饰品 UpdateAccessory()  → ShieldEffect.Apply() 向 _defs 注入定义
    ///   3. PostUpdateMiscEffects() → 汇总 MaxShield；若护盾 > 0 执行 OnActive 加成
    ///   4. PostUpdate()            → 衰减、恢复计时、护盾恢复
    ///   5. ModifyHitByX()          → 受击时重置恢复计时器，标记吸收
    ///   6. OnHurt()                → 用护盾抵消实际伤害，Heal 还回已扣 HP
    ///
    /// ── 架构要点 ──────────────────────────────────────────────────────────────────
    ///   · 全量吸收：护盾血量足够时，玩家不损失任何 HP
    ///   · 多来源加算：MaxShield = 所有激活 def 的 GetMaxShield() 之和
    ///   · 衰减取最大：多个 def 同时生效时取各自 DecayPerSecond 的最大值
    ///   · 恢复取最优：取最短延迟、最快速率
    ///   · 颜色取首个：优先使用第一个提供了颜色的 def 的颜色
    /// </summary>
    public class ShieldPlayer : ModPlayer
    {
        // ── 公开状态 ──────────────────────────────────────────────────────
        public float CurrentShield  { get; private set; }
        public float MaxShield      { get; private set; }

        /// <summary>
        /// UI 专用的平滑显示值，每帧向 CurrentShield lerp，消除整数截断引起的"每秒跳变"感。
        /// 只用于绘制，不参与任何战斗计算。
        /// </summary>
        public float DisplayShield  { get; private set; }

        /// <summary>护盾气泡主色（由激活的 def 决定，用于 Luminance 滤镜）。</summary>
        public Color  ShieldColor    { get; private set; } = new Color(80, 160, 255);
        /// <summary>护盾气泡边缘色（菲涅尔发光）。</summary>
        public Color  ShieldEdgeColor { get; private set; } = new Color(160, 220, 255);

        // ── 内部状态 ──────────────────────────────────────────────────────
        private readonly List<ShieldDefinition> _defs = new();

        private float _decayPerSecond  = 0f;    // 合并衰减速率
        private int   _rechargeDelay   = 0;     // 合并恢复延迟（帧）
        private float _rechargeRate    = 0f;    // 合并恢复速率

        private int   _hitTimer        = int.MaxValue; // 距上次受击的帧数
        private bool  _wasShielded     = false;        // 上帧是否有护盾（用于 OnBreak 检测）
        private bool  _absorbingHit    = false;        // 本帧是否正在吸收受击

        // Luminance 滤镜名称（与 fx 文件名对应）
        public const string FilterName = "TestMod.EnergyShieldFilter";

        // 滤镜动画：平滑淡入淡出的护盾强度（0~1）
        private float _filterStrength  = 0f;
        private const float FadeSpeed  = 0.06f; // 每帧变化量

        // ── ShieldEffect.Apply 注入入口 ───────────────────────────────────
        internal void AddDefinition(ShieldDefinition def) => _defs.Add(def);

        /// <summary>
        /// 直接追加护盾量（紧急护盾等特殊触发使用）。
        /// 不裁剪到 MaxShield——下一帧 PostUpdateMiscEffects 会统一处理上限。
        /// </summary>
        internal void AddShield(float amount) => CurrentShield += amount;

        // ════════════════════════════════════════════════════════════════
        //   ResetEffects — 每帧清空，等待饰品重新注入
        // ════════════════════════════════════════════════════════════════
        public override void ResetEffects()
        {
            _defs.Clear();
        }

        // ════════════════════════════════════════════════════════════════
        //   PostUpdateMiscEffects — 汇总 MaxShield + 执行 OnActive 加成
        //   （UpdateAccessory 在此之前完成，_defs 已就绪）
        // ════════════════════════════════════════════════════════════════
        public override void PostUpdateMiscEffects()
        {
            if (_defs.Count == 0)
            {
                MaxShield     = 0f;
                _decayPerSecond = 0f;
                _rechargeDelay  = 0;
                _rechargeRate   = 0f;
                return;
            }

            float newMax     = 0f;
            float maxDecay   = 0f;
            int   minDelay   = int.MaxValue;
            float maxRate    = 0f;
            bool  colorSet   = false;

            foreach (var def in _defs)
            {
                newMax   += def.GetMaxShield?.Invoke(Player) ?? 0f;
                maxDecay  = Math.Max(maxDecay, def.DecayPerSecond);
                minDelay  = Math.Min(minDelay, def.RechargeDelayFrames);
                float rate = def.GetRechargePerSecond?.Invoke(Player) ?? def.RechargePerSecond;
                maxRate   = Math.Max(maxRate,  rate);

                // 颜色：取第一个提供了非默认颜色的 def
                if (!colorSet && def.ShieldColor != default)
                {
                    ShieldColor     = def.ShieldColor;
                    ShieldEdgeColor = def.ShieldEdgeColor;
                    colorSet        = true;
                }

                // 护盾存活时执行属性加成
                if (CurrentShield > 0f)
                    def.OnActive?.Invoke(Player);
            }

            MaxShield       = Math.Max(0f, newMax);
            _decayPerSecond = maxDecay;
            _rechargeDelay  = minDelay == int.MaxValue ? 0 : minDelay;
            _rechargeRate   = maxRate;

            // 护盾上限缩小时同步裁剪当前值
            if (CurrentShield > MaxShield)
                CurrentShield = MaxShield;

            // 护盾存活时免疫击退（全局效果，不依赖具体 def）
            if (CurrentShield > 0f)
                Player.noKnockback = true;
        }

        // ════════════════════════════════════════════════════════════════
        //   PostUpdate — 衰减、恢复计时、护盾恢复、Luminance 滤镜更新
        // ════════════════════════════════════════════════════════════════
        public override void PostUpdate()
        {
            if (MaxShield <= 0f)
            {
                CurrentShield = 0f;
                UpdateShieldFilter();
                return;
            }

            // 自动衰减（护盾 > 0 才衰减）
            if (CurrentShield > 0f && _decayPerSecond > 0f)
            {
                CurrentShield -= _decayPerSecond / 60f;
                if (CurrentShield < 0f) { CurrentShield = 0f; TriggerOnBreak(); }
            }

            // 恢复计时器递增（每帧 +1；ModifyHitByX 受击时归零）
            if (_hitTimer < int.MaxValue) _hitTimer++;

            // 恢复条件：护盾不满 + 延迟已过
            if (CurrentShield < MaxShield && _hitTimer >= _rechargeDelay)
            {
                if (_rechargeRate >= float.MaxValue / 2f)
                    CurrentShield = MaxShield;           // 瞬间恢复
                else
                    CurrentShield = Math.Min(MaxShield, CurrentShield + _rechargeRate / 60f);
            }

            _wasShielded = CurrentShield > 0f;

            // 平滑显示值：每帧向真实值靠近，避免 UI 出现整数截断引起的跳变感
            // lerp 系数 0.18：约 5 帧追上，视觉流畅但无明显滞后
            DisplayShield = MathHelper.Lerp(DisplayShield, CurrentShield, 0.18f);

            UpdateShieldFilter();
        }

        // ════════════════════════════════════════════════════════════════
        //   ModifyHitByX — 受击时重置恢复计时、标记本帧吸收
        // ════════════════════════════════════════════════════════════════
        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers)
        {
            if (CurrentShield > 0f) { _hitTimer = 0; _absorbingHit = true; }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers)
        {
            if (CurrentShield > 0f) { _hitTimer = 0; _absorbingHit = true; }
        }

        // ════════════════════════════════════════════════════════════════
        //   OnHurt — 护盾全量吸收伤害，还回已扣 HP
        //   tModLoader 保证此时 info.Damage 为实际扣除的 HP 量
        // ════════════════════════════════════════════════════════════════
        public override void OnHurt(Player.HurtInfo info)
        {
            if (!_absorbingHit || CurrentShield <= 0f || info.Damage <= 0) { _absorbingHit = false; return; }
            _absorbingHit = false;

            // 护盾最多吸收 info.Damage 点
            int absorbed  = (int)Math.Min(info.Damage, CurrentShield);
            CurrentShield -= absorbed;

            // 把被护盾吸收掉的 HP 还回去（玩家实际零扣血）
            if (absorbed > 0)
                Player.statLife = Math.Min(Player.statLifeMax2, Player.statLife + absorbed);

            // 护盾归零触发破盾效果
            if (CurrentShield <= 0f && _wasShielded)
                TriggerOnBreak();
        }

        // ════════════════════════════════════════════════════════════════
        //   破盾触发
        // ════════════════════════════════════════════════════════════════
        private void TriggerOnBreak()
        {
            _wasShielded = false;
            foreach (var def in _defs)
                def.OnBreak?.Invoke(Player);
        }

        // ════════════════════════════════════════════════════════════════
        //   Luminance 滤镜更新（仅本地玩家 & 非服务端）
        //   使用与 TimeStopFilter 相同的 ManagedScreenFilter 模式
        // ════════════════════════════════════════════════════════════════
        private void UpdateShieldFilter()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) return;

            // 目标强度：护盾存活时 1，否则 0（淡入淡出）
            float targetStrength = (MaxShield > 0f && CurrentShield > 0f)
                ? CurrentShield / MaxShield
                : 0f;

            _filterStrength = _filterStrength < targetStrength
                ? Math.Min(targetStrength, _filterStrength + FadeSpeed)
                : Math.Max(targetStrength, _filterStrength - FadeSpeed);

            if (_filterStrength <= 0f) return;

            if (!ShaderManager.TryGetFilter(FilterName, out ManagedScreenFilter filter))
                return;

            filter.SetFocusPosition(Player.Center);
            filter.TrySetParameter("time",          Main.GlobalTimeWrappedHourly);
            filter.TrySetParameter("shieldStrength", _filterStrength);
            // 护盾半径约等于玩家宽度的 2.5 倍（像素）
            filter.TrySetParameter("shieldRadius",  Player.width * 2.5f);
            filter.TrySetParameter("shieldColor",   ShieldColor.ToVector3());
            filter.TrySetParameter("edgeColor",     ShieldEdgeColor.ToVector3());
            filter.Activate();
        }
    }
}
