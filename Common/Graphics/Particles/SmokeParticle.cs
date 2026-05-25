using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TestMod.Common.Graphics.Particles
{
    public sealed class SmokeParticle : TestModParticle
    {
        private readonly float startScale;
        private readonly float endScale;
        private readonly float drag;

        public override string AtlasTextureName => TestModParticleTextures.SoftSmoke;

        public override BlendState BlendState => BlendState.AlphaBlend;

        public SmokeParticle(Vector2 position, Vector2 velocity, Color color, int lifetime, float startScale = 0.4f, float endScale = 1f, float drag = 0.98f)
        {
            Position = position;
            Velocity = velocity;
            DrawColor = color;
            Lifetime = SafeLifetime(lifetime);
            this.startScale = startScale;
            this.endScale = endScale;
            this.drag = drag;
            RotationSpeed = 0.012f;
            Scale = Vector2.One * startScale;
        }

        public override void Update()
        {
            float completion = LifetimeRatio;
            Velocity *= drag;
            Rotation += RotationSpeed;
            Scale = Vector2.One * MathHelper.Lerp(startScale, endScale, completion);
            Opacity = FadeInOut(completion, 0.2f, 0.25f) * 0.55f;
        }
    }
}
