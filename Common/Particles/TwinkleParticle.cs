using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TestMod.Common.Particles
{
    public sealed class TwinkleParticle : TestModParticle
    {
        private readonly float baseScale;
        private readonly float pulseStrength;
        private readonly float drag;

        public override string AtlasTextureName => TestModParticleTextures.FourPointStar;

        public override BlendState BlendState => BlendState.Additive;

        public TwinkleParticle(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale = 1f, float pulseStrength = 0.24f, float drag = 0.96f)
        {
            Position = position;
            Velocity = velocity;
            DrawColor = color;
            Lifetime = SafeLifetime(lifetime);
            baseScale = scale;
            this.pulseStrength = pulseStrength;
            this.drag = drag;
            RotationSpeed = 0.04f;
            Scale = Vector2.One * scale;
        }

        public override void Update()
        {
            float completion = LifetimeRatio;
            float pulse = 1f + SineFade(completion) * pulseStrength;
            Velocity *= drag;
            Rotation += RotationSpeed;
            Scale = Vector2.One * (baseScale * pulse);
            Opacity = FadeInOut(completion, 0.12f, 0.5f);
        }
    }
}
