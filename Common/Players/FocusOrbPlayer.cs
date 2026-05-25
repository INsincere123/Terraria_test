using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Common.Graphics.Particles;
using TestMod.Common.Mechanics.AccessoryEffects;
using TestMod.Content.Items.DamageTypes;

namespace TestMod.Common.Players
{
    public class FocusOrbPlayer : ModPlayer
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        private const int   ForcedCritCount   = 7;        // 强制暴击次数
        private const int   ForcedHomingCount = 7;        // 强制追踪弹幕次数
        private const float CritBoost         = 100f;     // 施加的暴击率加成
        private const float ExtraHitRatio     = 2f;   // 本该暴击时：触发伤害的 200% 真实伤害
        private const int   CooldownDuration  = 60 * 30; // 冷却时间：30 秒（buff 计时）

        private const int FocusMoteSpawnChance = 3;
        private const int FocusStreakSpawnChance = 3;
        private const int FocusBloomSpawnChance = 10;
        private const float FocusHandOffsetX = 12f;
        private const float FocusHandOffsetY = -4f;
        private const float FocusStreakLengthMin = 0.86f;
        private const float FocusStreakLengthMax = 1.32f;
        private const float FocusStreakThicknessMin = 0.38f;
        private const float FocusStreakThicknessMax = 0.58f;

        public bool HasFocusOrb;

        public override void ResetEffects()
        {
            HasFocusOrb = false;
        }

        public override void PostUpdateMiscEffects()
        {
            var mp = Player.GetModPlayer<OmniEffectsPlayer>();
            if (mp.ForcedCritRemaining <= 0)
                return;

            SummonCritPlayer.Enable(Player);
            SpawnFocusChargeParticles();
        }

        private void SpawnFocusChargeParticles()
        {
            if (Main.dedServ)
                return;

            Vector2 handPos = GetFocusHandPosition();
            Color color = GetFocusColor(Main.rand.NextFloat(0.18f));

            if (Main.rand.NextBool(FocusMoteSpawnChance))
            {
                Vector2 spawnPosition = handPos + Main.rand.NextVector2Circular(7f, 5f);
                Vector2 velocity = new Vector2(Player.direction * Main.rand.NextFloat(0.05f, 0.38f), Main.rand.NextFloat(-0.58f, -0.18f))
                    + Main.rand.NextVector2Circular(0.22f, 0.18f);

                new TwinkleParticle(spawnPosition, velocity, color, Main.rand.Next(22, 34), Main.rand.NextFloat(0.18f, 0.32f), 0.18f, 0.95f).Spawn();
            }

            if (Main.rand.NextBool(FocusStreakSpawnChance))
            {
                Vector2 orbitDirection = Main.rand.NextVector2CircularEdge(1f, 0.72f);
                Vector2 spawnPosition = handPos + orbitDirection * Main.rand.NextFloat(5f, 11f);
                Vector2 tangent = orbitDirection.RotatedBy(Player.direction * MathHelper.PiOver2);
                Vector2 velocity = tangent * Main.rand.NextFloat(0.66f, 1.08f)
                    + new Vector2(Player.direction * 0.12f, -0.2f);

                SpawnFocusStreak(spawnPosition, velocity, color * 0.86f, Main.rand.Next(24, 36));

                if (Main.rand.NextBool(3))
                    SpawnFocusStreak(spawnPosition - tangent * Main.rand.NextFloat(3f, 6f), velocity * 0.72f, color * 0.52f, Main.rand.Next(18, 28), 0.74f);
            }

            if (Main.rand.NextBool(FocusBloomSpawnChance))
            {
                Vector2 spawnPosition = handPos + Main.rand.NextVector2Circular(4f, 4f);
                Color bloomColor = Color.Lerp(color, Color.White, 0.25f);

                new BloomParticle(spawnPosition, Main.rand.NextVector2Circular(0.08f, 0.08f), bloomColor * 0.44f, Main.rand.Next(18, 28), Main.rand.NextFloat(0.07f, 0.12f), Main.rand.NextFloat(0.13f, 0.2f), 0.96f).Spawn();
            }
        }

        private Vector2 GetFocusHandPosition()
            => Player.MountedCenter
            + new Vector2(Player.direction * FocusHandOffsetX, FocusHandOffsetY)
            + Player.velocity * 0.35f;

        private static void SpawnFocusStreak(Vector2 position, Vector2 velocity, Color color, int lifetime, float scaleMultiplier = 1f)
        {
            new StreakParticle(
                position,
                velocity,
                color,
                lifetime,
                Main.rand.NextFloat(FocusStreakLengthMin, FocusStreakLengthMax) * scaleMultiplier,
                Main.rand.NextFloat(FocusStreakThicknessMin, FocusStreakThicknessMax) * scaleMultiplier,
                0.94f).Spawn();
        }

        private static Color GetFocusColor(float offset = 0f)
        {
            float hue = (Main.GlobalTimeWrappedHourly * 0.28f + offset) % 1f;
            Color prismatic = Main.hslToRgb(hue, 0.86f, 0.62f);
            Color focusBlue = new Color(92, 220, 255);
            Color focusGold = new Color(255, 214, 96);
            float goldPulse = (MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f) + 1f) * 0.5f;
            Color focusTint = Color.Lerp(focusBlue, focusGold, goldPulse * 0.32f);

            return Color.Lerp(prismatic, focusTint, 0.42f);
        }

        public void TryActivate()
        {
            if (!HasFocusOrb)
                return;
            if (Player.HasBuff(ModContent.BuffType<FocusOrbCooldownBuff>()))
                return;

            ForcedCritEffect.Apply(Player, new ForcedCritConfig
            {
                ForcedCritCount = ForcedCritCount,
                ForcedHomingCount = ForcedHomingCount,
                CritBoost = CritBoost,
                ExtraHit = new ExtraHitConfig
                {
                    HitDamageRatio = ExtraHitRatio,
                    FixedClass = TrueDamageClass.Instance,
                    FollowWeapon = false,
                    UseCrit = false,
                    Knockback = 0f,
                    NoPlayerInteraction = false,
                    CombatTextColor = new Color(0, 220, 255),
                },
            });

            Player.AddBuff(ModContent.BuffType<FocusOrbCooldownBuff>(), CooldownDuration);
            SoundEngine.PlaySound(SoundID.DrumKick, Player.Center);
            SpawnFocusActivationBurst();
        }

        private void SpawnFocusActivationBurst()
        {
            if (Main.dedServ)
                return;

            Vector2 handPos = GetFocusHandPosition();
            Color burstColor = GetFocusColor(0.08f);

            new RingPulseParticle(handPos, burstColor * 0.78f, 24, 0.12f, 0.54f).Spawn();
            new BloomParticle(handPos, Vector2.Zero, Color.Lerp(burstColor, Color.White, 0.35f) * 0.56f, 28, 0.12f, 0.26f, 0.94f).Spawn();

            for (int i = 0; i < 7; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(0.55f, 1.2f);
                Color sparkColor = GetFocusColor(i * 0.07f);
                new TwinkleParticle(handPos + velocity * 3f, velocity * 0.38f, sparkColor, Main.rand.Next(20, 32), Main.rand.NextFloat(0.2f, 0.36f), 0.16f, 0.93f).Spawn();
            }

            for (int i = 0; i < 5; i++)
            {
                Vector2 direction = Main.rand.NextVector2CircularEdge(1f, 0.72f);
                Vector2 velocity = direction * Main.rand.NextFloat(1.15f, 1.9f);
                Color trailColor = GetFocusColor(i * 0.11f);
                SpawnFocusStreak(handPos + direction * Main.rand.NextFloat(4f, 9f), velocity, trailColor * 0.9f, Main.rand.Next(26, 40), Main.rand.NextFloat(1.05f, 1.42f));
            }
        }
    }
}
