using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using TestMod.Content.Buffs;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 玩家侧时停状态管理。
    /// </summary>
    public class TimeStopPlayer : ModPlayer
    {
        // ============ 可调参数 ============
        public const int Duration = 60 * 3;          // 时停持续 3 秒
        public const int Cooldown = 60 * 30;      // 冷却 30 秒

        // —— shader 视觉参数 ——
        // 圆洞稳态半径（归一化到屏幕短边，0.18 ≈ 屏幕高度的 18%）
        public const float HoleRadius01 = 0.18f;
        // 过渡带宽度（归一化）
        public const float HoleFadeWidth01 = 0.20f;
        // 圆外最大灰化强度 0~1，0.5 = 半灰，更柔和
        public const float MaxGrayStrength = 0.72f;

        // 转场动画速度（每帧归一化半径的变化）
        public const float HoleAnimSpeed = 0.06f;
        // 灰化强度淡入淡出速度（每帧）
        public const float GrayFadeSpeed = 0.04f;

        public const string FilterName = "TestMod.TimeStopFilter";

        // ============ 运行时状态 ============
        public bool TimeStopActive;
        public int TimeStopTimer;
        public int TimeStopCooldown;
        public bool HasTimeStopAbility;

        // ── 时缓状态 ──────────────────────────────────────────────
        // 使用方式（饰品 / 按键触发）：
        //   player.GetModPlayer<TimeStopPlayer>().HasTimeSlowAbility = true;
        //   player.GetModPlayer<TimeStopPlayer>().TryActivateTimeSlow();
        public bool TimeSlowActive;
        public int  TimeSlowTimer;
        public int  TimeSlowCooldown;
        public bool HasTimeSlowAbility;

        /// <summary>速度系数：0.3 = 敌人/射弹降至 30% 速度。</summary>
        public float TimeSlowFactor   = 0.3f;
        /// <summary>持续帧数（默认 5 秒）。</summary>
        public int   TimeSlowDuration = 60 * 5;
        /// <summary>冷却帧数（默认 20 秒）。</summary>
        public int   TimeSlowCooldownMax = 60 * 20;

        // 圆洞当前半径（归一化）：从 1.5（覆盖整屏外）→ HoleRadius01（小圆）
        private float currentRadius01 = 1.5f;
        // 圆外灰化强度当前值：从 0 → MaxGrayStrength
        private float currentGrayStrength;

        // ============ 生命周期 ============

        public override void ResetEffects()
        {
            HasTimeStopAbility = false;
        }

        public override void OnEnterWorld()
        {
            currentRadius01 = 1.5f;
            currentGrayStrength = 0f;
        }

        public override void PostUpdate()
        {
            if (TimeStopActive)
            {
                TimeStopTimer--;
                if (TimeStopTimer <= 0)
                    EndTimeStop();

                //Player.AddBuff(ModContent.BuffType<TimeStoppedBuff>(), 2);
            }

            if (TimeStopCooldown > 0)
            {
                TimeStopCooldown--;
                Player.AddBuff(ModContent.BuffType<TimeStopCDBuff>(), TimeStopCooldown);
            }

            // 时缓计时
            if (TimeSlowActive)
            {
                TimeSlowTimer--;
                if (TimeSlowTimer <= 0)
                    EndTimeSlow();
            }

            if (TimeSlowCooldown > 0)
                TimeSlowCooldown--;

            UpdateShaderFilter();
        }

        // ============ 激活与结束 ============

        public bool TryActivateTimeSlow()
        {
            if (TimeSlowActive)   return false;
            if (TimeStopActive)   return false;  // 时停期间不能叠时缓
            if (TimeSlowCooldown > 0) return false;
            if (!HasTimeSlowAbility) return false;

            TimeSlowActive  = true;
            TimeSlowTimer   = TimeSlowDuration;
            TimeSlowCooldown = TimeSlowCooldownMax;
            return true;
        }

        public void EndTimeSlow()
        {
            TimeSlowActive = false;
            TimeSlowTimer  = 0;
        }

        public bool TryActivateTimeStop()
        {
            if (TimeStopActive) return false;
            if (TimeStopCooldown > 0) return false;
            if (!HasTimeStopAbility) return false;

            TimeStopActive = true;
            TimeStopTimer = Duration;
            TimeStopCooldown = Cooldown;

            currentRadius01 = 1.5f; // 从全屏外开始收缩

            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.2f }, Player.Center);
            return true;
        }

        public void EndTimeStop()
        {
            if (!TimeStopActive) return;

            TimeStopActive = false;
            TimeStopTimer = 0;

            SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.8f, Pitch = 0.2f }, Player.Center);
        }

        public override void OnRespawn()
        {
            if (TimeStopActive)
            {
                TimeStopActive = false;
                TimeStopTimer  = 0;
            }
            if (TimeSlowActive)
            {
                TimeSlowActive = false;
                TimeSlowTimer  = 0;
            }
            currentRadius01 = 1.5f;
            currentGrayStrength = 0f;
        }

        // ============ Shader 控制 ============

        private void UpdateShaderFilter()
        {
            if (Main.dedServ) return;
            if (Player.whoAmI != Main.myPlayer) return;

            // ── 圆洞半径动画 ──
            float targetRadius = TimeStopActive ? HoleRadius01 : 1.5f;
            if (currentRadius01 > targetRadius)
            {
                currentRadius01 -= HoleAnimSpeed;
                if (currentRadius01 < targetRadius) currentRadius01 = targetRadius;
            }
            else if (currentRadius01 < targetRadius)
            {
                currentRadius01 += HoleAnimSpeed;
                if (currentRadius01 > targetRadius) currentRadius01 = targetRadius;
            }

            // ── 灰化强度淡入淡出 ──
            float targetGray = TimeStopActive ? MaxGrayStrength : 0f;
            if (currentGrayStrength < targetGray)
            {
                currentGrayStrength += GrayFadeSpeed;
                if (currentGrayStrength > targetGray) currentGrayStrength = targetGray;
            }
            else if (currentGrayStrength > targetGray)
            {
                currentGrayStrength -= GrayFadeSpeed;
                if (currentGrayStrength < targetGray) currentGrayStrength = targetGray;
            }

            // 完全没效果时跳过激活省性能
            if (currentGrayStrength <= 0f && currentRadius01 >= 1.5f) return;

            if (!ShaderManager.TryGetFilter(FilterName, out ManagedScreenFilter filter))
                return;

            // 关键：必须用 SetFocusPosition 传世界坐标
            // Luminance 在 Apply 时会自动把 focusPosition / screenPosition / screenSize 设到 shader
            filter.SetFocusPosition(Player.Center);

            filter.TrySetParameter("radius01", currentRadius01);
            filter.TrySetParameter("fadeWidth01", HoleFadeWidth01);
            filter.TrySetParameter("grayStrength", currentGrayStrength);

            filter.Activate();
        }
    }
}