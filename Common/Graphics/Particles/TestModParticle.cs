using System;
using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace TestMod.Common.Graphics.Particles
{
    public abstract class TestModParticle : Particle
    {
        protected static float SineFade(float completion)
            => MathF.Sin(MathHelper.Clamp(completion, 0f, 1f) * MathHelper.Pi);

        protected static float FadeInOut(float completion, float fadeInPortion = 0.18f, float fadeOutStart = 0.55f)
        {
            completion = MathHelper.Clamp(completion, 0f, 1f);
            float fadeIn = MathHelper.Clamp(completion / fadeInPortion, 0f, 1f);
            float fadeOut = 1f - MathHelper.Clamp((completion - fadeOutStart) / MathF.Max(0.001f, 1f - fadeOutStart), 0f, 1f);
            return fadeIn * fadeOut;
        }

        protected static int SafeLifetime(int lifetime) => Math.Max(1, lifetime);

        protected void DrawAtlas(SpriteBatch spriteBatch, Color color, Vector2 scale, float rotation, SpriteEffects effects = SpriteEffects.None)
        {
            if (Texture is null || Opacity <= 0f)
                return;

            spriteBatch.Draw(
                Texture,
                Position - Main.screenPosition,
                Frame,
                color * Opacity,
                rotation,
                null,
                scale,
                effects);
        }
    }
}
