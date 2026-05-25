using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TestMod.Common.Graphics.Particles
{
    public sealed class BloomParticle : TestModParticle
    {
        private readonly float startScale;
        private readonly float endScale;
        private readonly float drag;

        public override string AtlasTextureName => TestModParticleTextures.SoftBloom;

        public override BlendState BlendState => BlendState.Additive;

        public BloomParticle(Vector2 position, Vector2 velocity, Color color, int lifetime, float startScale, float endScale = -1f, float drag = 0.94f)
        {
            Position = position;
            Velocity = velocity;
            DrawColor = color;
            Lifetime = SafeLifetime(lifetime);
            this.startScale = startScale;
            this.endScale = endScale < 0f ? startScale : endScale;
            this.drag = drag;
            Scale = Vector2.One * startScale;
        }

        public override void Update()
        {
            float completion = LifetimeRatio;
            Velocity *= drag;
            Scale = Vector2.One * MathHelper.Lerp(startScale, endScale, completion);
            Opacity = SineFade(completion);
        }
    }
}
