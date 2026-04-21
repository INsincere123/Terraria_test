using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace 武器test.Projectiles.Minions
{
    public class SiriusBeam : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BulletHighVelocity;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.timeLeft = 600;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.MaxUpdates = 3;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.penetrate = 2;
            Projectile.stopsDealingDamageAfterPenetrateHits = true;
        }

        public override void AI()
        {
            NPC target = FindTarget(5000f);

            if (target != null && Projectile.localNPCImmunity[target.whoAmI] <= 0)
            {
                float speed = Projectile.timeLeft < 300 ? 14f : 10f;
                Projectile.velocity = Vector2.Lerp(
                    Projectile.velocity,
                    (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * speed,
                    0.08f);
            }
            else if (Projectile.velocity.Length() < 6f)
            {
                Projectile.velocity *= 1.02f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                DustID.BlueTorch, 0f, 0f, 100, default, 1.1f);
            Main.dust[d].noGravity = true;
            Main.dust[d].velocity *= 0.3f;

            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.6f);
        }

        private NPC FindTarget(float range)
        {
            NPC result = null;
            float minDist = range;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < minDist)
                {
                    minDist = dist;
                    result = npc;
                }
            }
            return result;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);

            int trailLen = ProjectileID.Sets.TrailCacheLength[Type];
            for (int i = trailLen - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - i / (float)trailLen;

                Color outer = Color.DarkSlateBlue * fade * 0.8f;
                float outerScale = 14f * fade;
                Main.spriteBatch.Draw(pixel, drawPos, src, outer, 0f, new Vector2(0.5f), outerScale, SpriteEffects.None, 0f);

                Color inner = Color.SkyBlue * fade;
                float innerScale = 8f * fade;
                Main.spriteBatch.Draw(pixel, drawPos, src, inner, 0f, new Vector2(0.5f), innerScale, SpriteEffects.None, 0f);
            }

            Vector2 corePos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(pixel, corePos, src, Color.Lerp(Color.SkyBlue, Color.White, 0.5f),
                0f, new Vector2(0.5f), 12f, SpriteEffects.None, 0f);

            return false;
        }
    }
}