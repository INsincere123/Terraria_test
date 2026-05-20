using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Projectiles.Minions
{
    public class QuasarJetProjectile : ModProjectile
    {
        private const int TrailCacheLength = 30;
        private const float OuterTrailWidth = 24f;
        private const float InnerTrailWidth = 7f;
        private const float TrailSegmentOverlap = 10f;
        private const float HeadScale = 0.46f;
        private const float GlowScale = 0.38f;
        private const float GlowOpacity = 0.22f;
        private static readonly Color JetColor = new(55, 160, 255);
        private static readonly Color HotCoreColor = new(235, 250, 255);

        public override string Texture => "Terraria/Images/Projectile_12";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailCacheLength;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.timeLeft = 90;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2;
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

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            Player owner = Main.player[Projectile.owner];
            var summonCritPlayer = owner.GetModPlayer<SummonCritPlayer>();

            if (!summonCritPlayer.Enabled)
                SummonCritPlayer.TryApplySummonCrit(owner, Projectile, ref modifiers, requireEnabled: false);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            DrawTexturedTrail();
            DrawHead();
            return false;
        }

        private void DrawTexturedTrail()
        {
            Texture2D trailTexture = QuasarJetVisualAssetSystem.TrailTexture;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                Vector2 start = Projectile.oldPos[i];
                if (start == Vector2.Zero)
                    continue;

                Vector2 startCenter = start + Projectile.Size * 0.5f;
                Vector2 endCenter = i == 0 ? Projectile.Center : Projectile.oldPos[i - 1] + Projectile.Size * 0.5f;
                Vector2 segment = endCenter - startCenter;
                float length = segment.Length();
                if (length <= 1f)
                    continue;

                float completion = 1f - i / (float)Projectile.oldPos.Length;
                float opacity = completion * completion;
                Vector2 direction = segment / length;
                float drawLength = length + TrailSegmentOverlap;
                Vector2 drawPosition = (startCenter + endCenter) * 0.5f + direction * (TrailSegmentOverlap * 0.5f) - Main.screenPosition;
                float rotation = segment.ToRotation();

                float widthFade = MathHelper.Lerp(0.35f, 1f, completion);
                DrawTrailLayer(trailTexture, drawPosition, rotation, drawLength, OuterTrailWidth * widthFade, JetColor * (0.34f * opacity));
                DrawTrailLayer(trailTexture, drawPosition, rotation, drawLength, InnerTrailWidth * widthFade, HotCoreColor * (0.82f * opacity));
            }
        }

        private static void DrawTrailLayer(Texture2D texture, Vector2 position, float rotation, float length, float width, Color color)
        {
            Rectangle frame = texture.Frame();
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 scale = QuasarJetVisualAssetSystem.HasTrailTexture
                ? new Vector2(length / frame.Width, width / frame.Height)
                : new Vector2(length, width);

            Main.spriteBatch.Draw(
                texture,
                position,
                frame,
                color,
                rotation,
                origin,
                scale,
                SpriteEffects.None,
                0f);
        }

        private void DrawHead()
        {
            Vector2 screenPosition = Projectile.Center - Main.screenPosition;
            float rotation = Projectile.velocity.ToRotation();

            if (QuasarJetVisualAssetSystem.HasGlowTexture)
                DrawCentered(QuasarJetVisualAssetSystem.GlowTexture, screenPosition, rotation, JetColor * GlowOpacity, GlowScale);
            else
                DrawFallbackGlow(screenPosition, rotation);

            if (QuasarJetVisualAssetSystem.HasHeadTexture)
                DrawCentered(QuasarJetVisualAssetSystem.HeadTexture, screenPosition, rotation, Color.White, HeadScale);
            else
                DrawFallbackHead(screenPosition, rotation);
        }

        private static void DrawCentered(Texture2D texture, Vector2 position, float rotation, Color color, float scale)
        {
            Rectangle frame = texture.Frame();
            Main.spriteBatch.Draw(
                texture,
                position,
                frame,
                color,
                rotation,
                frame.Size() * 0.5f,
                scale,
                SpriteEffects.None,
                0f);
        }

        private static void DrawFallbackGlow(Vector2 position, float rotation)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Main.spriteBatch.Draw(
                pixel,
                position,
                new Rectangle(0, 0, 1, 1),
                JetColor * GlowOpacity,
                rotation,
                new Vector2(0.5f),
                new Vector2(26f, 8f),
                SpriteEffects.None,
                0f);
        }

        private static void DrawFallbackHead(Vector2 position, float rotation)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Main.spriteBatch.Draw(
                pixel,
                position,
                new Rectangle(0, 0, 1, 1),
                Color.White,
                rotation,
                new Vector2(0.5f),
                new Vector2(18f, 5f),
                SpriteEffects.None,
                0f);
        }
    }
}
