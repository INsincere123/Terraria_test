using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using TestMod.Common.Utilities;
using TestMod.Content.Projectiles.Dashes;

namespace TestMod.Common.Mechanics.Dashes
{
    // 长冲刺：22像素×22帧，额外位移约30.25格；独立按键支持水平或垂直方向。
    // 起步清水平速度，保留垂直惯性；结束追加70%冲刺速度。实际总位移还包含原版运动。
    public class LongDash : InertialDash<LongDashStrike>
    {
        public const float DashSpeed       = 22f;   // 每帧像素 (22 = 1.375 格/帧)
        public const int   DashFrames      = 22;    // 持续帧数 (22 帧 × 22像素 = 484像素 ≈ 30.25 格（仅额外位移）)
        public const int   CooldownFrames  = 15;    // 结束后冷却帧数

        // 结束时追加冲刺速度的比例；0表示不追加，不会清除已有速度
        public const float ExitInertiaRatio = 0.7f; // 0=不追加, 1=追加完整冲刺速度

        // 撞击伤害
        public const int   ContactDamage     = 1350;
        public const float ContactKnockback  = 15f;
        public const int   ContactCritDenom  = 4;   // 1/4 = 25% 暴击
        public const int   MaxHitsPerDash    = 16;

        // 无敌帧
        public const int   StartIFrames    = 22;    // 起步补足的最小免疫帧，不缩短已有较长免疫
        public const int   ActiveIFrames   = 12;
        public const int   ContactIFrames  = 30;

        // 视觉效果
        private static readonly Color DashDustColor = new(255, 198, 82);
        public const int   ContactDustCount = 35;

        private static readonly DashSettings Configuration = new(DashSpeed, ExitInertiaRatio,
            ContactDamage, ContactKnockback, ContactCritDenom, MaxHitsPerDash,
            StartIFrames, ActiveIFrames, ContactIFrames);
        protected override DashSettings Settings => Configuration;
        public override string Id => "LongDash";
        public override int DashDuration => DashFrames;
        public override int DashCooldown => CooldownFrames;

        protected override void PlayStartEffects(Player player)
        {
            SoundEngine.PlaySound(SoundID.Item24, player.Center);
            DustUtils.SpawnCosmicBurst(player.Center, 40, DashDustColor, 1.8f, 4.8f, 1.1f);
        }

        protected override void SpawnTrail(Player player, Vector2 unitDir)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 position = player.Center + Main.rand.NextVector2Circular(player.width * 0.45f, player.height * 0.45f);
                Vector2 velocity = -unitDir * Main.rand.NextFloat(2.2f, 4.6f) + Main.rand.NextVector2Circular(0.8f, 0.8f);
                DustUtils.SpawnImpactLine(position, velocity, DashDustColor, Main.rand.NextFloat(0.86f, 1.18f), 50);
            }
        }

        protected override void SpawnContactEffects(NPC target)
            => DustUtils.SpawnImpactBurst(target.Center, ContactDustCount, DashDustColor, 2.6f, 7.2f, 1.25f);
    }
}
