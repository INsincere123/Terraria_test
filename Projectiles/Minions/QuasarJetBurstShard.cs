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
    public class QuasarJetBurstShard : ModProjectile
    {
        private const int TrailCacheLength = 30;
        private const float OuterTrailWidth = 17f;  //外层尾迹宽度
        private const float InnerTrailWidth = 5f;   //内层核心宽度 
        private const float TrailSegmentOverlap = 6f;   //尾迹段之间的重叠距离，避免出现明显断层
        private const float MaxTrailLength = 180f;   //尾迹最大长度
        private const float HeadScale = 0.42f;      //弹头缩放
        private const float GlowScale = 0.38f;      //光晕缩放
        private const float GlowOpacity = 0.18f;    //光晕不透明度
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
            Projectile.timeLeft = 120;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.MaxUpdates = 1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.25f, 0.45f, 0.95f);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            Player owner = Main.player[Projectile.owner];
            var summonCritPlayer = owner.GetModPlayer<SummonCritPlayer>();
            modifiers.ScalingArmorPenetration += 0.9f;
            //modifiers.FinalDamage *= 1.11f;
            if (!summonCritPlayer.Enabled)
                SummonCritPlayer.TryApplySummonCrit(owner, Projectile, ref modifiers, requireEnabled: false);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Projectile.Kill();
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
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float rotation = direction.ToRotation();
            float trailLength = GetStraightTrailLength(direction);

            if (trailLength <= 2f)
                return;

            Vector2 drawPosition = Projectile.Center - direction * (trailLength * 0.5f + TrailSegmentOverlap) - Main.screenPosition;
            float drawLength = trailLength + TrailSegmentOverlap * 2f;

            DrawTrailLayer(trailTexture, drawPosition, rotation, drawLength, OuterTrailWidth, JetColor * 0.22f);
            DrawTrailLayer(trailTexture, drawPosition, rotation, drawLength, InnerTrailWidth, HotCoreColor * 0.58f);
        }

        private float GetStraightTrailLength(Vector2 direction)
        {
            float length = Projectile.velocity.Length() * Math.Max(Projectile.oldPos.Length, 1);

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                float projectedDistance = Vector2.Dot(Projectile.Center - oldCenter, direction);
                if (projectedDistance > 0f)
                    length = projectedDistance;

                break;
            }

            return MathHelper.Clamp(length, 20f, MaxTrailLength);     //限制尾迹长度在合理范围内，避免过长或过短导致视觉问题
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
                new Vector2(30f, 10f),
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
