using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Compatibility;
using TestMod.Common.DataStructures;
using TestMod.Common.Utilities;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 每帧收集装备注入的定义；数值状态集中于 ShieldPoolState。
    /// 普通恢复统一为 11 秒延迟、每秒恢复总上限的 1/6。
    /// 开始恢复后保持脆弱状态，直到普通池回满；临时护盾不能解除该状态。
    /// </summary>
    public partial class EnergyShieldPlayer : ModPlayer
    {
        public const int RechargeDelayFrames = ShieldPoolState.RechargeDelayFrames;
        public const int RechargeDurationFrames = ShieldPoolState.RechargeDurationFrames;
        public float CurrentShield => _state.Current;
        public float MaxShield => _state.Maximum;
        public float NormalCurrentShield => _state.NormalShield;
        public float NormalMaxShield => _state.NormalMax;
        public float TemporaryCurrentShield => _state.TemporaryShield;
        public bool IsRechargeFragile => _state.IsFragile;
        public bool IsRecharging => _state.IsRecovering;
        /// <summary>UI 只读查询，不在绘制端维护恢复计时。</summary>
        public int RechargeWaitRemainingFrames => Math.Max(0, RechargeDelayFrames - _state.HitTimer);
        public float ShieldHitFlashStrength => _shieldHitFlashTimer / (float)ShieldHitFlashFrames;
        public float DisplayShield { get; private set; }
        public Color ShieldColor { get; private set; } = new(80, 160, 255);
        public Color ShieldEdgeColor { get; private set; } = new(160, 220, 255);

        private readonly ShieldPoolState _state = new();
        private readonly List<ShieldDefinition> _defs = new(2);
        private float _decayPerSecond;
        private byte _equipmentMask;
        private bool _wasDead;
        internal bool IsAuthority => Main.netMode != NetmodeID.Server && Player.whoAmI == Main.myPlayer;

        public const string FilterName = "TestMod.EnergyShieldFilter";
        private float _filterStrength;
        private const float FadeSpeed = 0.06f;
        private int _shieldHitFlashTimer;
        private const int ShieldHitFlashFrames = 10;
        private static readonly Color ShieldTextColor = new(64, 224, 255);

        internal void AddDefinition(ShieldDefinition def) => _defs.Add(def);

        internal void AddTemporaryShield(float amount, int duration)
        {
            if (!IsAuthority) return;
            _state.AddTemporary(amount, duration);
            SendShieldState(force: true);
        }

        public override void ResetEffects() => _defs.Clear();

        public override void PostUpdateMiscEffects()
        {
            float maximum = 0f;
            _decayPerSecond = 0f;
            bool colorSet = false;
            foreach (ShieldDefinition def in _defs)
            {
                maximum += def.GetMaxShield?.Invoke(Player) ?? 0f;
                _decayPerSecond = Math.Max(_decayPerSecond, def.DecayPerSecond);
                if (!colorSet && def.ShieldColor != default)
                {
                    ShieldColor = def.ShieldColor;
                    ShieldEdgeColor = def.ShieldEdgeColor;
                    colorSet = true;
                }
            }

            if (IsAuthority)
            {
                byte mask = (byte)((_defs.Count > 0 ? 1 : 0) |
                    (Player.GetModPlayer<SuperEnergyShieldPlayer>().IsEquipped ? 2 : 0));
                bool equipmentChanged = mask != _equipmentMask;
                _equipmentMask = mask;
                _wasDead = false;
                float oldMax = _state.NormalMax;
                bool oldFragile = _state.IsFragile;
                _state.SetEquipment(maximum, (mask & 2) != 0);
                if (equipmentChanged || oldMax != _state.NormalMax || oldFragile != _state.IsFragile)
                    SendShieldState(force: true);
            }

            // 远端只依据收到的余额决定加成，不独立恢复或触发临时护盾。
            if (CurrentShield > 0f)
            {
                foreach (ShieldDefinition def in _defs)
                    def.OnActive?.Invoke(Player);
                Player.noKnockback = true;
            }
        }

        public override void PostUpdate()
        {
            if (IsAuthority)
            {
                bool wasFragile = _state.IsFragile;
                bool wasRecovering = _state.IsRecovering;
                bool hadTemporary = _state.TemporaryFrames > 0;
                var result = _state.Tick(_decayPerSecond);
                if (result.Broke) TriggerOnBreak();
                if (result.RecoveryStarted) SpawnShieldRecoveringText();
                SendShieldState(force: result.Broke || wasFragile != _state.IsFragile ||
                    wasRecovering != _state.IsRecovering || hadTemporary != (_state.TemporaryFrames > 0));
            }

            DisplayShield = MathHelper.Lerp(DisplayShield, CurrentShield, 0.18f);
            if (_shieldHitFlashTimer > 0) _shieldHitFlashTimer--;
            UpdateShieldFilter();
        }

        public override void UpdateDead()
        {
            _defs.Clear();
            DisplayShield = _filterStrength = 0f;
            _shieldHitFlashTimer = 0;
            if (IsAuthority && !_wasDead)
            {
                _wasDead = true;
                _equipmentMask = 0;
                _state.Die();
                SendShieldState(force: true);
            }
        }

        public override void TransformDrawData(ref PlayerDrawSet drawInfo)
        {
            if (Player.dead || MaxShield <= 0f || CurrentShield <= 0f)
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
            if (IsAuthority && (_state.Initialized || NormalMaxShield > 0f))
                modifiers.ModifyHurtInfo += AbsorbDamageWithShield;
        }

        private void AbsorbDamageWithShield(ref Player.HurtInfo info)
        {
            if (info.Cancelled || info.Damage <= 0) return;

            int incomingDamage = info.Damage;
            var result = _state.TakeHit(incomingDamage);
            if (result.Absorbed > 0f)
            {
                _shieldHitFlashTimer = ShieldHitFlashFrames;
                PlayShieldHitSound();
                SpawnShieldDamageText((int)Math.Ceiling(result.Absorbed));
                DustUtils.SpawnShieldHit(Player, ShieldEdgeColor, info.HitDirection);
            }
            if (result.Broke) TriggerOnBreak();

            if (result.LifeDamage == 0)
            {
                info.Cancelled = true;
                info.SoundDisabled = true;
                info.DustDisabled = true;
                GiveVanillaHurtIFrames(info, incomingDamage);
            }
            else
            {
                // 这里收到的已是防御/减伤结算后的伤害；不再次 Hurt，不重复计算减伤。
                info.Damage = result.LifeDamage;
            }
            SendShieldState(force: true);
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

            TextRenderingBridge.Spawn(new DynamicWorldTextRequest(
                $"-{absorbed}",
                Player.Center + new Vector2(0f, -Player.height * 0.62f),
                TestModTextStyles.EnergyShieldDamage,
                ShieldTextColor,
                scale: 1f,
                seed: unchecked((int)Main.GameUpdateCount + absorbed * 31 + Player.whoAmI * 997)));
        }

        private void SpawnShieldBreakText()
        {
            if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer)
                return;

            TextRenderingBridge.Spawn(new DynamicWorldTextRequest(
                "护盾已破碎",
                Player.Center + new Vector2(0f, -Player.height * 0.95f),
                TestModTextStyles.EnergyShieldBreak,
                ShieldTextColor,
                scale: 1f,
                seed: unchecked((int)Main.GameUpdateCount + Player.whoAmI * 1297)));
        }

        private void SpawnShieldRecoveringText()
        {
            if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer)
                return;

            TextRenderingBridge.Spawn(new DynamicWorldTextRequest(
                "\u62A4\u76FE\u6B63\u5728\u6062\u590D",
                Player.Center + new Vector2(0f, -Player.height * 0.95f),
                TestModTextStyles.EnergyShieldBreak,
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
            SpawnShieldBreakText();
            DustUtils.SpawnShieldBurst(Player.Center, 28, ShieldEdgeColor, 2.6f, 6.8f, 1.1f);
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
