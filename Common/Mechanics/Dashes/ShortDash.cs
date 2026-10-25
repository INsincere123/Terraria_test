using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using TestMod.Common.Utilities;
using TestMod.Content.Projectiles.Dashes;

namespace TestMod.Common.Mechanics.Dashes
{
    // 短冲刺：18像素×10帧，额外位移约11.25格；独立按键支持水平或垂直方向。
    // 起步清水平速度，保留垂直惯性；结束追加70%冲刺速度。实际总位移还包含原版运动。
    public class ShortDash : InertialDash<ShortDashStrike>
    {
        public const float DashSpeed      = 18f;  // 每帧像素 (18 × 10帧 = 180px ≈ 11.25格)
        public const int   DashFrames     = 10;    // 持续帧数
        public const int   CooldownFrames = 15;   // 冷却帧数

        // 结束时追加冲刺速度的比例；0表示不追加，不会清除已有速度
        public const float ExitInertiaRatio = 0.7f;

        // 撞击伤害
        public const int   ContactDamage    = 1350;
        public const float ContactKnockback = 15f;
        public const int   ContactCritDenom = 4;   // 1/N 概率暴击, 设 0 禁用
        public const int   MaxHitsPerDash   = 8;

        // 无敌帧
        public const int StartIFrames   = 16;
        public const int ActiveIFrames  = 12;
        public const int ContactIFrames = 30;

        // 视觉 (蓝白系, 与长冲刺金火焰区分)
        private static readonly Color DashDustColor = new(130, 205, 255);
        public const int ContactDustCount = 35;

        private static readonly DashSettings Configuration = new(DashSpeed, ExitInertiaRatio,
            ContactDamage, ContactKnockback, ContactCritDenom, MaxHitsPerDash,
            StartIFrames, ActiveIFrames, ContactIFrames);
        protected override DashSettings Settings => Configuration;
        public override string Id => "ShortDash";
        public override int DashDuration => DashFrames;
        public override int DashCooldown => CooldownFrames;

        protected override void PlayStartEffects(Player player)
        {
            SoundEngine.PlaySound(SoundID.Item8, player.Center);
            DustUtils.SpawnTemporalBurst(player.Center, 34, DashDustColor, 1.6f, 4.4f, 0.95f);
        }

        protected override void SpawnTrail(Player player, Vector2 unitDir)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 position = player.Center + Main.rand.NextVector2Circular(player.width * 0.45f, player.height * 0.45f);
                Vector2 velocity = -unitDir * Main.rand.NextFloat(2f, 4.2f) + Main.rand.NextVector2Circular(0.7f, 0.7f);
                DustUtils.SpawnTemporalShard(position, velocity, new Color(140, 215, 255), Main.rand.NextFloat(0.78f, 1.08f), 55);
            }
        }

        protected override void SpawnContactEffects(NPC target)
            => DustUtils.SpawnImpactBurst(target.Center, ContactDustCount, DashDustColor, 2.2f, 6.4f, 1.05f);
    }
}
