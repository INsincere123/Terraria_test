using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 玩家侧时停状态管理。
    /// 时停是"全局世界状态"，但状态归属挂在玩家身上，便于多人隔离与现有 Effect 体系一致。
    /// </summary>
    public class TimeStopPlayer : ModPlayer
    {
        // ===== 可调参数 =====
        /// <summary>时停持续时间（帧）。60帧=1秒</summary>
        public const int Duration = 180;          // 3 秒
        /// <summary>时停冷却时间（帧）</summary>
        public const int Cooldown = 60 * 30;      // 30 秒

        // ===== 运行时状态 =====
        /// <summary>当前是否处于时停状态</summary>
        public bool TimeStopActive;
        /// <summary>时停剩余持续帧数</summary>
        public int TimeStopTimer;
        /// <summary>时停冷却剩余帧数</summary>
        public int TimeStopCooldown;

        /// <summary>
        /// 本帧玩家是否拥有时停能力（由饰品/盔甲在 UpdateAccessory/UpdateEquips 中置 true）。
        /// 每帧在 ResetEffects 重置。
        /// </summary>
        public bool HasTimeStopAbility;

        public override void ResetEffects()
        {
            HasTimeStopAbility = false;
        }

        public override void PostUpdate()
        {
            // 持续时间倒计时
            if (TimeStopActive)
            {
                TimeStopTimer--;
                if (TimeStopTimer <= 0)
                    EndTimeStop();

                // 激活中图标：每帧续 2 帧，始终显示
                //Player.AddBuff(ModContent.BuffType<TimeStoppedBuff>(), 2);
            }

            // 冷却倒计时
            if (TimeStopCooldown > 0)
            {
                TimeStopCooldown--;

                // 冷却中图标：buffTime 直接对齐剩余冷却帧数，进度条自然倒数
                Player.AddBuff(ModContent.BuffType<TimeStopCDBuff>(), TimeStopCooldown);
            }
        }

        /// <summary>
        /// 尝试激活时停。条件：拥有能力、未在冷却、未在时停中。
        /// 返回是否成功激活。
        /// </summary>
        public bool TryActivateTimeStop()
        {
            if (TimeStopActive) return false;
            if (TimeStopCooldown > 0) return false;
            if (!HasTimeStopAbility) return false;

            TimeStopActive = true;
            TimeStopTimer = Duration;
            TimeStopCooldown = Cooldown;

            // 开启音效（玩家中心）
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f, Pitch = -0.2f }, Player.Center);
            // SoundID.Item122 = 时空之矛挥舞声，类似"时间撕裂"，作为时停起手音效

            return true;
        }

        /// <summary>
        /// 结束时停（计时归零或被外部终止时调用）。
        /// </summary>
        public void EndTimeStop()
        {
            if (!TimeStopActive) return;

            TimeStopActive = false;
            TimeStopTimer = 0;

            // 结束音效
            SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.8f, Pitch = 0.2f }, Player.Center);
            // SoundID.Item117 = 影焰刀效果声，作为时停结束音效
        }

        /// <summary>
        /// 玩家死亡时清除时停状态，避免死后还卡着。
        /// </summary>
        public override void OnRespawn()
        {
            if (TimeStopActive)
            {
                TimeStopActive = false;
                TimeStopTimer = 0;
            }
        }
    }
}