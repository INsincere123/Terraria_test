using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace TestMod.Common.Graphics.Particles
{
    public sealed class ShardParticle : TestModParticle
    {
        private readonly float startScale;
        private readonly float drag;

        public override string AtlasTextureName => TestModParticleTextures.GlowyShard;

        public override BlendState BlendState => BlendState.Additive;

        public ShardParticle(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale = 1f, float rotationSpeed = 0.08f, float drag = 0.94f)
        {
            Position = position;
            Velocity = velocity;
            DrawColor = color;
            Lifetime = SafeLifetime(lifetime);
            startScale = scale;
            Rotation = velocity == Vector2.Zero ? 0f : velocity.ToRotation();
            RotationSpeed = rotationSpeed;
            this.drag = drag;
            Scale = Vector2.One * scale;
        }

        public override void Update()
        {
            float completion = LifetimeRatio;
            Velocity *= drag;
            Rotation += RotationSpeed;
            Scale = Vector2.One * (startScale * MathHelper.Lerp(1f, 0.35f, completion));
            Opacity = FadeInOut(completion, 0.08f, 0.45f);
        }
    }
}
