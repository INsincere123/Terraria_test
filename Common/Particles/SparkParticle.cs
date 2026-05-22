using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace TestMod.Common.Particles
{
    public sealed class SparkParticle : TestModParticle
    {
        private readonly float lengthScale;
        private readonly float widthScale;
        private readonly float drag;

        public override string AtlasTextureName => TestModParticleTextures.SharpSpark;

        public override BlendState BlendState => BlendState.Additive;

        public SparkParticle(Vector2 position, Vector2 velocity, Color color, int lifetime, float lengthScale = 1f, float widthScale = 1f, float drag = 0.9f)
        {
            Position = position;
            Velocity = velocity;
            DrawColor = color;
            Lifetime = SafeLifetime(lifetime);
            this.lengthScale = lengthScale;
            this.widthScale = widthScale;
            this.drag = drag;
            Rotation = velocity == Vector2.Zero ? 0f : velocity.ToRotation();
            Scale = new Vector2(lengthScale, widthScale);
        }

        public override void Update()
        {
            float completion = LifetimeRatio;
            Velocity *= drag;
            Rotation = Velocity == Vector2.Zero ? Rotation : Velocity.ToRotation();
            Scale = new Vector2(
                lengthScale * MathHelper.Lerp(1f, 0.42f, completion),
                widthScale * MathHelper.Lerp(0.75f, 1.15f, SineFade(completion)));
            Opacity = FadeInOut(completion, 0.08f, 0.35f);
        }
    }
}
