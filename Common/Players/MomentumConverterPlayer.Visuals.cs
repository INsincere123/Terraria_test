using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.ModLoader;
using TestMod.Common.Graphics.Particles;
using TestMod.Content.Dusts;

namespace TestMod.Common.Players
{
    public sealed partial class MomentumConverterPlayer
    {
        private const int ShadowCount = 7;
        private const int ShadowDuration = 18;
        private readonly Vector2[] shadowPositions = new Vector2[ShadowCount];
        private int shadowTimer;
        private bool drawingConversionShadow;
        private Color shadowTint;
        private static readonly Color MomentumBlue = new(70, 211, 255);
        private static readonly Color MomentumPurple = new(175, 100, 255);
        private static readonly SoundStyle ConversionSound = new("TestMod/Assets/Sounds/MomentumConverter_Sound");

        private void PlayConversionVisuals(Vector2 center, Vector2 originalVelocity, Vector2 convertedVelocity)
        {
            if (Main.dedServ || Player.dead)
                return;

            float originalAngle = originalVelocity.ToRotation();
            float turn = MathHelper.WrapAngle(convertedVelocity.ToRotation() - originalAngle);
            float stepLength = Math.Clamp(originalVelocity.Length() * 0.75f, 5f, 16f);
            Vector2 cursor = center - Player.Size * 0.5f;
            // 从转向后的切线向后积分，形成由旧方向弯向新方向的短弧；仅影响残影。
            for (int i = ShadowCount - 1; i >= 0; i--)
            {
                float completion = (i + 0.5f) / ShadowCount;
                float angle = originalAngle + turn * completion;
                cursor -= angle.ToRotationVector2() * stepLength;
                shadowPositions[i] = cursor;
                Vector2 particlePosition = cursor + Player.Size * 0.5f;
                Color color = Color.Lerp(MomentumBlue, MomentumPurple, completion);
                new StreakParticle(particlePosition, angle.ToRotationVector2() * 1.5f,
                    color, 20, 0.9f, 0.45f).Spawn();
            }
            shadowTimer = ShadowDuration;

            for (int i = 0; i < 12; i++)
            {
                Color color = Color.Lerp(MomentumBlue, MomentumPurple, i / 11f);
                Vector2 sparkVelocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(1.5f, 4f);
                new SparkParticle(center, sparkVelocity, color, Main.rand.Next(14, 23), 0.55f, 0.35f).Spawn();
            }
            for (int i = 0; i < 4; i++)
            {
                Dust gear = Dust.NewDustPerfect(center + Main.rand.NextVector2Circular(10f, 10f),
                    ModContent.DustType<MomentumGearDust>(), Main.rand.NextVector2Circular(2.2f, 2.2f),
                    newColor: Color.Lerp(MomentumBlue, MomentumPurple, i / 3f), Scale: Main.rand.NextFloat(0.35f, 0.5f));
                gear.noGravity = true;
            }
            SoundEngine.PlaySound(ConversionSound with { Volume = 0.36f, Pitch = 3.3f }, center);
        }

        public override void DrawPlayer(Camera camera)
        {
            if (Main.dedServ || Player.dead || shadowTimer <= 0 || drawingConversionShadow)
                return;

            float fade = shadowTimer / (float)ShadowDuration;
            drawingConversionShadow = true;
            try
            {
                for (int i = 0; i < ShadowCount; i++)
                {
                    float completion = i / (float)(ShadowCount - 1);
                    shadowTint = Color.Lerp(MomentumBlue, MomentumPurple, completion);
                    float opacity = fade * MathHelper.Lerp(0.12f, 0.42f, completion);
                    Main.PlayerRenderer.DrawPlayer(camera, Player, shadowPositions[i], Player.fullRotation,
                        Player.fullRotationOrigin, 1f - opacity);
                }
            }
            finally
            {
                drawingConversionShadow = false;
            }
        }

        public override void TransformDrawData(ref PlayerDrawSet drawInfo)
        {
            if (!drawingConversionShadow)
                return;
            for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
            {
                DrawData data = drawInfo.DrawDataCache[i];
                data.color = new Color(data.color.ToVector4() * shadowTint.ToVector4());
                drawInfo.DrawDataCache[i] = data;
            }
        }
    }
}
