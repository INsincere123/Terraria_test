using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;

namespace TestMod.Projectiles
{
    public class TestProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_454";

        public const int FadeInTime = 24;
        public const int HoldTime = 180;
        public const int FadeOutTime = 60;
        public const int Lifetime = FadeInTime + HoldTime + FadeOutTime;
        public const float VisualRadius = 72f;
        public const float MinimumVisibleRadius = 2f;
        public const float MaxSpeed = 13f;

        public override void SetDefaults()
        {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI()
        {
            float opacity = GetOpacity();
            if (opacity < 0.35f)
                Projectile.friendly = false;

            Projectile.rotation += 0.045f * Projectile.direction;
            Projectile.velocity *= 0.985f;

            if (Projectile.velocity.Length() > MaxSpeed)
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * MaxSpeed;

            float visualRadius = GetVisualRadius();
            if (visualRadius > MinimumVisibleRadius)
                GravitationalLensSystem.RegisterBlackHole(Projectile.Center, visualRadius, opacity, new Color(245, 105, 61));

            Lighting.AddLight(Projectile.Center, 0.8f * opacity, 0.28f * opacity, 0.12f * opacity);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        private float GetOpacity()
        {
            float age = Lifetime - Projectile.timeLeft;
            float fadeIn = MathHelper.Clamp(age / FadeInTime, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / (float)FadeOutTime, 0f, 1f);
            return fadeIn * fadeOut;
        }

        private float GetVisualRadius()
        {
            float pulse = 1f + (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 5.6f + Projectile.whoAmI) * 0.08f;
            return VisualRadius * pulse * Projectile.scale * GetRadiusScale();
        }

        private float GetRadiusScale()
        {
            float age = Lifetime - Projectile.timeLeft;
            float fadeIn = MathHelper.Clamp(age / FadeInTime, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / (float)FadeOutTime, 0f, 1f);

            // 生成时从小到大展开，消失时从大到小塌缩。
            float appearScale = MathHelper.SmoothStep(0.15f, 1f, fadeIn);
            float disappearScale = MathHelper.SmoothStep(0f, 1f, fadeOut);
            return appearScale * disappearScale;
        }
    }
}
