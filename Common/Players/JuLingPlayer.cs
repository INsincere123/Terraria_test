using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;

namespace TestMod.Common.Players
{
    public class JuLingPlayer : ModPlayer
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        private const int ActiveDuration   = 60 * 5;   // 主动持续：5 秒（60帧/秒）
        private const int CooldownDuration = 60 * 45;  // 冷却时间：45 秒
        // ─────────────────────────────────────────────────────────────

        // 本帧是否穿着完整聚灵套（由 UpdateArmorSet 写入）
        public bool HasJuLingSet;

        public override void ResetEffects()
        {
            HasJuLingSet = false;
        }

        /// <summary>
        /// 尝试激活聚灵主动技能。由按键处理系统调用。
        /// 不满足条件时静默忽略；激活成功时播放音效。
        /// </summary>
        public void TryActivate()
        {
            if (!HasJuLingSet) return;
            if (Player.HasBuff(ModContent.BuffType<JuLingActiveBuff>()))   return; // 已激活
            if (Player.HasBuff(ModContent.BuffType<JuLingCooldownBuff>()))  return; // 冷却中

            Player.AddBuff(ModContent.BuffType<JuLingActiveBuff>(),  ActiveDuration);
            Player.AddBuff(ModContent.BuffType<JuLingCooldownBuff>(), CooldownDuration);

            // 激活音效（法力水晶拾取音，清脆的魔法音色）
            SoundEngine.PlaySound(SoundID.MaxMana, Player.Center);
        }
    }
}
