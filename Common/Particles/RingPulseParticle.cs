using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TestMod.Common.Particles
{
    public sealed class RingPulseParticle : TestModParticle
    {
        private readonly float startScale;
        private readonly float endScale;

        public override string AtlasTextureName => TestModParticleTextures.RingPulse;

        public override BlendState BlendState => BlendState.Additive;

        public RingPulseParticle(Vector2 position, Color color, int lifetime, float startScale = 0.25f, float endScale = 1f)
        {
            Position = position;
            Velocity = Vector2.Zero;
            DrawColor = color;
            Lifetime = SafeLifetime(lifetime);
            this.startScale = startScale;
            this.endScale = endScale;
            Scale = Vector2.One * startScale;
        }

        public override void Update()
        {
            float completion = LifetimeRatio;
            Scale = Vector2.One * MathHelper.Lerp(startScale, endScale, completion);
            Opacity = 1f - MathHelper.SmoothStep(0f, 1f, completion);
        }
    }
}
