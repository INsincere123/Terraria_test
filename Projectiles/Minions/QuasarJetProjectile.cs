using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Projectiles.Minions
{
    public class QuasarJetProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_12";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 18;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.timeLeft = 60;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.MaxUpdates = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.25f, 0.45f, 0.95f);

            if (Main.rand.NextBool(3))
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 80, new Color(70, 170, 255), 1.15f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 unit = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 origin = new(0.5f, 0.5f);

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                Vector2 oldPosition = Projectile.oldPos[i];
                if (oldPosition == Vector2.Zero)
                    continue;

                float completion = 1f - i / (float)Projectile.oldPos.Length;
                float width = MathHelper.Lerp(3f, 11f, completion);
                float length = MathHelper.Lerp(12f, 26f, completion);
                Color color = Color.Lerp(new Color(45, 95, 255), Color.White, completion) * (completion * 0.75f);

                Main.spriteBatch.Draw(
                    pixel,
                    oldPosition + Projectile.Size * 0.5f - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    color,
                    Projectile.rotation,
                    origin,
                    new Vector2(length, width),
                    SpriteEffects.None,
                    0f);
            }

            Main.spriteBatch.Draw(
                pixel,
                Projectile.Center - Main.screenPosition,
                new Rectangle(0, 0, 1, 1),
                Color.White,
                Projectile.rotation,
                origin,
                new Vector2(24f, 7f),
                SpriteEffects.None,
                0f);

            Main.spriteBatch.Draw(
                pixel,
                Projectile.Center - Main.screenPosition - unit * 8f,
                new Rectangle(0, 0, 1, 1),
                new Color(65, 175, 255) * 0.8f,
                Projectile.rotation,
                origin,
                new Vector2(40f, 14f),
                SpriteEffects.None,
                0f);

            return false;
        }
    }
}
