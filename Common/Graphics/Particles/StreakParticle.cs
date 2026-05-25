using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace TestMod.Common.Graphics.Particles
{
    public sealed class StreakParticle : TestModParticle
    {
        private readonly float length;
        private readonly float thickness;
        private readonly float drag;

        public override string AtlasTextureName => TestModParticleTextures.ThinStreak;

        public override BlendState BlendState => BlendState.Additive;

        public StreakParticle(Vector2 position, Vector2 velocity, Color color, int lifetime, float length = 1f, float thickness = 1f, float drag = 0.92f)
        {
            Position = position;
            Velocity = velocity;
            DrawColor = color;
            Lifetime = SafeLifetime(lifetime);
            this.length = length;
            this.thickness = thickness;
            this.drag = drag;
            Rotation = velocity == Vector2.Zero ? 0f : velocity.ToRotation();
            Scale = new Vector2(length, thickness);
        }

        public override void Update()
        {
            float completion = LifetimeRatio;
            Velocity *= drag;
            Rotation = Velocity == Vector2.Zero ? Rotation : Velocity.ToRotation();
            Scale = new Vector2(
                length * MathHelper.Lerp(1f, 0.25f, completion),
                thickness * MathHelper.Lerp(0.85f, 1.15f, SineFade(completion)));
            Opacity = FadeInOut(completion, 0.06f, 0.42f);
        }
    }
}
