using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;
using TestMod.Items.Accessories.Effects;
using TestMod.Items.DamageTypes;

namespace TestMod.Common.Players
{
    public class FocusOrbPlayer : ModPlayer
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        private const int   ForcedCritCount   = 6;        // 强制暴击次数
        private const int   ForcedHomingCount = 6;        // 强制追踪弹幕次数
        private const float CritBoost         = 100f;     // 施加的暴击率加成
        private const float ExtraHitRatio     = 1.2f;   // 本该暴击时：触发伤害的 120% 真实伤害
        private const int   CooldownDuration  = 60 * 30; // 冷却时间：30 秒（buff 计时）
        // ─────────────────────────────────────────────────────────────

        // 本帧是否装备了专注宝珠（UpdateAccessory 每帧写入，ResetEffects 重置）
        public bool HasFocusOrb;

        public override void ResetEffects()
        {
            HasFocusOrb = false;
        }

        // 有剩余次数时，在手部位置生成少量彩色粒子
        public override void PostUpdateMiscEffects()
        {
            var mp = Player.GetModPlayer<OmniEffectsPlayer>();
            if (mp.ForcedCritRemaining <= 0) return;
            if (!Main.rand.NextBool(4)) return; // 约 25% 概率/帧，~15 粒子/秒

            // 手部位置：持握武器侧，稍微偏上
            Vector2 handPos = Player.MountedCenter
                            + new Vector2(Player.direction * 12f, -4f)
                            + Main.rand.NextVector2Circular(5f, 5f);

            // 时间驱动色相循环（完整循环约 2 秒）
            float hue  = Main.GlobalTimeWrappedHourly * 0.5f % 1f;
            Color color = Main.hslToRgb(hue, 1f, 0.65f);

            Dust dust = Dust.NewDustPerfect(
                handPos,
                DustID.GoldFlame,                       // 金焰粒子，确保可见
                Velocity: Main.rand.NextVector2Circular(0.6f, 0.6f),
                newColor: color,                        // newColor 参数覆盖颜色
                Scale: 1.0f
            );
            dust.noGravity = true;
            dust.noLight   = false;
        }

        /// <summary>
        /// 尝试激活专注宝珠主动效果。由按键系统调用，不满足条件时静默忽略。
        /// </summary>
        public void TryActivate()
        {
            if (!HasFocusOrb) return;
            if (Player.HasBuff(ModContent.BuffType<FocusOrbCooldownBuff>())) return;

            ForcedCritEffect.Apply(Player, new ForcedCritConfig
            {
                ForcedCritCount   = ForcedCritCount,
                ForcedHomingCount = ForcedHomingCount,
                CritBoost         = CritBoost,
                ExtraHit          = new ExtraHitConfig
                {
                    HitDamageRatio      = ExtraHitRatio,
                    FixedClass          = TrueDamageClass.Instance,
                    FollowWeapon        = false,
                    UseCrit             = false,
                    Knockback           = 0f,
                    NoPlayerInteraction = false,
                    CombatTextColor     = new Color(0, 220, 255), // 青色伤害数字
                },
            });

            Player.AddBuff(ModContent.BuffType<FocusOrbCooldownBuff>(), CooldownDuration);
            SoundEngine.PlaySound(SoundID.DrumKick, Player.Center);
        }
    }
}
