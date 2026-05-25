using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DynamicText;
using TestMod.Common.DataStructures;

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
    ///   5. ModifyHurt()            → 受击时扣除护盾并在完全吸收时取消 Hurt
    ///
    /// ── 架构要点 ──────────────────────────────────────────────────────────────────
    ///   · 全量吸收：护盾血量足够时，玩家不损失任何 HP
    ///   · 多来源加算：MaxShield = 所有激活 def 的 GetMaxShield() 之和
    ///   · 衰减取最大：多个 def 同时生效时取各自 DecayPerSecond 的最大值
    ///   · 恢复取最保守：取最长延迟、最慢速率
    ///   · 颜色取首个：优先使用第一个提供了颜色的 def 的颜色
    /// </summary>
    public class EnergyShieldPlayer : ModPlayer
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
        private bool  _wasRecovering   = false;

        // Luminance 滤镜名称（与 fx 文件名对应）
        public const string FilterName = "TestMod.EnergyShieldFilter";

        // 滤镜动画：平滑淡入淡出的护盾强度（0~1）
        private float _filterStrength  = 0f;
        private const float FadeSpeed  = 0.06f; // 每帧变化量
        private int _shieldHitFlashTimer;
        private const int ShieldHitFlashFrames = 10;
        private static readonly Color ShieldTextColor = new(64, 224, 255);

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
            int   maxDelay   = 0;
            float minRate    = float.MaxValue;
            bool  colorSet   = false;

            foreach (var def in _defs)
            {
                newMax   += def.GetMaxShield?.Invoke(Player) ?? 0f;
                maxDecay  = Math.Max(maxDecay, def.DecayPerSecond);
                maxDelay  = Math.Max(maxDelay, def.RechargeDelayFrames);
                float rate = def.GetRechargePerSecond?.Invoke(Player) ?? def.RechargePerSecond;
                minRate   = Math.Min(minRate,  rate);

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
            _rechargeDelay  = maxDelay;
            _rechargeRate   = minRate == float.MaxValue ? 0f : minRate;

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
                _wasRecovering = false;
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
            bool isRecovering = false;
            if (CurrentShield < MaxShield && _hitTimer >= _rechargeDelay)
            {
                isRecovering = true;
                if (!_wasRecovering)
                    SpawnShieldRecoveringText();

                if (_rechargeRate >= float.MaxValue / 2f)
                    CurrentShield = MaxShield;           // 瞬间恢复
                else
                    CurrentShield = Math.Min(MaxShield, CurrentShield + _rechargeRate / 60f);
            }
            _wasRecovering = isRecovering;

            _wasShielded = CurrentShield > 0f;

            // 平滑显示值：每帧向真实值靠近，避免 UI 出现整数截断引起的跳变感
            // lerp 系数 0.18：约 5 帧追上，视觉流畅但无明显滞后
            DisplayShield = MathHelper.Lerp(DisplayShield, CurrentShield, 0.18f);

            if (_shieldHitFlashTimer > 0)
                _shieldHitFlashTimer--;

            UpdateShieldFilter();
        }

        public override void TransformDrawData(ref PlayerDrawSet drawInfo)
        {
            if (MaxShield <= 0f || CurrentShield <= 0f)
                return;

            float shieldRatio = MathHelper.Clamp(CurrentShield / MaxShield, 0f, 1f);
            Color baseOverlayColor = ShieldColor * MathHelper.Lerp(0.086f, 0.150f, shieldRatio);

            int drawCount = drawInfo.DrawDataCache.Count;
            for (int i = 0; i < drawCount; i++)
            {
                DrawData data = drawInfo.DrawDataCache[i];
                if (data.texture is null)
                    continue;

                DrawData baseOverlay = data;
                baseOverlay.color = baseOverlayColor;
                drawInfo.DrawDataCache.Add(baseOverlay);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //   ModifyHurt — 在真正 Hurt 前扣除护盾
        // ════════════════════════════════════════════════════════════════
        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (MaxShield <= 0f)
                return;

            _hitTimer = 0; // 受击始终重置计时器，护盾破碎后再被打也要重新等待

            if (CurrentShield > 0f)
                modifiers.ModifyHurtInfo += AbsorbDamageWithShield;
        }

        private void AbsorbDamageWithShield(ref Player.HurtInfo info)
        {
            if (CurrentShield <= 0f || info.Damage <= 0)
                return;

            int incomingDamage = info.Damage;
            int absorbed       = (int)Math.Min(incomingDamage, CurrentShield);
            if (absorbed <= 0)
                return;

            CurrentShield = Math.Max(0f, CurrentShield - absorbed);
            _shieldHitFlashTimer = ShieldHitFlashFrames;
            PlayShieldHitSound();
            SpawnShieldDamageText(absorbed);

            if (CurrentShield <= 0f && _wasShielded)
                TriggerOnBreak();

            // 命中时只要还有护盾，本次 Hurt 就完全由护盾接住。
            // 即使伤害超过剩余护盾，也只打空护盾，不把溢出伤害传给玩家。
            info.Cancelled     = true;
            info.SoundDisabled = true;
            info.DustDisabled  = true;
            GiveVanillaHurtIFrames(info, incomingDamage);
        }

        private void PlayShieldHitSound()
        {
            SoundStyle sound = SoundID.Shatter;
            sound.Volume = 0.7f;
            sound.Pitch = 0.2f;
            sound.PitchVariance = 0.25f;
            SoundEngine.PlaySound(sound, Player.Center);
        }

        private void SpawnShieldDamageText(int absorbed)
        {
            if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer)
                return;

            DynamicWorldTextSystem.Spawn(new DynamicWorldTextRequest(
                $"-{absorbed}",
                Player.Center + new Vector2(0f, -Player.height * 0.62f),
                DynamicTextStyleRegistry.EnergyShieldDamage,
                ShieldTextColor,
                scale: 1f,
                seed: unchecked((int)Main.GameUpdateCount + absorbed * 31 + Player.whoAmI * 997)));
        }

        private void SpawnShieldBreakText()
        {
            if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer)
                return;

            DynamicWorldTextSystem.Spawn(new DynamicWorldTextRequest(
                "护盾已破碎",
                Player.Center + new Vector2(0f, -Player.height * 0.95f),
                DynamicTextStyleRegistry.EnergyShieldBreak,
                ShieldTextColor,
                scale: 1f,
                seed: unchecked((int)Main.GameUpdateCount + Player.whoAmI * 1297)));
        }

        private void SpawnShieldRecoveringText()
        {
            if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer)
                return;

            DynamicWorldTextSystem.Spawn(new DynamicWorldTextRequest(
                "\u62A4\u76FE\u6B63\u5728\u6062\u590D",
                Player.Center + new Vector2(0f, -Player.height * 0.95f),
                DynamicTextStyleRegistry.EnergyShieldBreak,
                ShieldTextColor,
                scale: 0.82f,
                seed: unchecked((int)Main.GameUpdateCount + Player.whoAmI * 1423)));
        }

        private void GiveVanillaHurtIFrames(Player.HurtInfo info, int damage)
        {
            int time = info.PvP
                ? 8
                : damage == 1
                    ? Player.longInvince ? 40 : 20
                    : Player.longInvince ? 80 : 40;

            switch (info.CooldownCounter)
            {
                case -1:
                    Player.immune = true;
                    Player.immuneTime = Math.Max(Player.immuneTime, time);
                    break;

                case 0:
                case 1:
                case 3:
                case 4:
                    if (info.CooldownCounter < Player.hurtCooldowns.Length)
                        Player.hurtCooldowns[info.CooldownCounter] = Math.Max(Player.hurtCooldowns[info.CooldownCounter], time);
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //   破盾触发
        // ════════════════════════════════════════════════════════════════
        private void TriggerOnBreak()
        {
            _wasShielded = false;
            SpawnShieldBreakText();
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

            float shieldRatio = (MaxShield > 0f && CurrentShield > 0f)
                ? CurrentShield / MaxShield
                : 0f;

            // 护盾很低时也保留最低可见度，让玩家明确知道自己仍处于护盾罩内。
            float targetStrength = shieldRatio > 0f
                ? MathHelper.Lerp(0.28f, 1f, shieldRatio)
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
            filter.TrySetParameter("hitFlash",      _shieldHitFlashTimer / (float)ShieldHitFlashFrames);
            // 护盾半径约等于玩家宽度的 2.5 倍（像素）
            filter.TrySetParameter("shieldRadius",  Player.width * 2.5f);
            filter.TrySetParameter("shieldColor",   ShieldColor.ToVector3());
            filter.TrySetParameter("edgeColor",     ShieldEdgeColor.ToVector3());
            filter.Activate();
        }
    }
}
