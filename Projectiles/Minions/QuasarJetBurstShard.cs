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
        private const int TrailCacheLength = 40; // 增加尾迹缓存长度以平滑轨迹
        private const float OuterTrailWidth = 10f; // 减小外层尾迹宽度以使尾迹更窄，更加光滑
        private const float InnerTrailWidth = 6f; // 同样地，减小内层核心宽度以达到类似效果
        private const float TrailSegmentOverlap = 16f; // 增加尾迹段之间的重叠距离，减少突然中断
        private const float MaxTrailLength = 199f; // 减小尾迹的最大长度，使尾迹更早消失，更加光滑
        private const float HeadScale = 0.83f;      //弹头缩放
        private const float GlowScale = 0.5f;      //光晕缩放
        private const float GlowOpacity = 0.14f;    //光晕不透明度
        private const float ConnectorOuterOpacity = 0.24f;
        private const float ConnectorCoreOpacity = 0.58f;
        private const float HomingRange = 1200f;
        private const float HomingTurnStrength = 0.085f;
        private const int HomingDelay = 6;
        private const int PlasmaFlameCount = 2;
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
            TryCurveTowardTarget();
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.34f, 0.62f, 1.25f);

            for (int i = 0; i < PlasmaFlameCount; i++)
                SpawnPlasmaFlame();
        }

        private void TryCurveTowardTarget()
        {
            if (Projectile.timeLeft > 120 - HomingDelay)
                return;

            NPC target = GetHomingTarget();
            if (target is null)
                return;

            float speed = Projectile.velocity.Length();
            if (speed <= 0.01f)
                return;

            Vector2 desiredVelocity = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity) * speed;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, HomingTurnStrength).SafeNormalize(desiredVelocity) * speed;
        }

        private NPC GetHomingTarget()
        {
            int encodedTarget = (int)Projectile.ai[0] - 1;
            if (encodedTarget >= 0 && encodedTarget < Main.maxNPCs)
            {
                NPC lockedTarget = Main.npc[encodedTarget];
                if (lockedTarget.CanBeChasedBy(Projectile) && Projectile.Distance(lockedTarget.Center) <= HomingRange * 1.35f)
                    return lockedTarget;
            }

            NPC bestTarget = null;
            float bestDistanceSq = HomingRange * HomingRange;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float distanceSq = Vector2.DistanceSquared(npc.Center, Projectile.Center);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestTarget = npc;
            }

            return bestTarget;
        }

        private void SpawnPlasmaFlame()
        {
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
            Vector2 position = Projectile.Center
                - direction * Main.rand.NextFloat(8f, 28f)
                + normal * Main.rand.NextFloat(-8f, 8f);
            Vector2 velocity = -direction * Main.rand.NextFloat(1.6f, 4.6f)
                + normal * Main.rand.NextFloat(-2.1f, 2.1f);

            int dustType = Main.rand.NextBool(3) ? DustID.Electric : DustID.BlueTorch;
            Dust dust = Dust.NewDustPerfect(
                position,
                dustType,
                velocity,
                35,
                Color.Lerp(JetColor, HotCoreColor, Main.rand.NextFloat(0.25f, 0.8f)),
                Main.rand.NextFloat(1.1f, 1.9f));
            dust.noGravity = true;
            dust.velocity *= Main.rand.NextFloat(0.82f, 1.16f);
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
            Vector2 newerCenter = Projectile.Center;
            float accumulatedLength = 0f;

            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    break;

                Vector2 olderCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                float segmentLength = Vector2.Distance(newerCenter, olderCenter);
                if (segmentLength <= 1f)
                    continue;

                float remainingLength = MaxTrailLength - accumulatedLength;
                if (remainingLength <= 0f)
                    break;

                if (segmentLength > remainingLength)
                    olderCenter = Vector2.Lerp(newerCenter, olderCenter, remainingLength / segmentLength);

                Vector2 segmentDirection = (newerCenter - olderCenter).SafeNormalize(Projectile.velocity.SafeNormalize(Vector2.UnitX));
                float drawLength = Vector2.Distance(newerCenter, olderCenter) + TrailSegmentOverlap;
                float fade = 1f - MathHelper.Clamp(accumulatedLength / MaxTrailLength, 0f, 1f);
                float widthFactor = MathHelper.Lerp(0.38f, 1f, fade);
                Vector2 drawCenter = (newerCenter + olderCenter) * 0.5f - Main.screenPosition;
                float rotation = segmentDirection.ToRotation();

                DrawConnectorLayer(drawCenter, rotation, drawLength, OuterTrailWidth * widthFactor * 0.72f, JetColor * (ConnectorOuterOpacity * fade));
                DrawConnectorLayer(drawCenter, rotation, drawLength, InnerTrailWidth * widthFactor * 0.82f, HotCoreColor * (ConnectorCoreOpacity * fade));
                DrawTrailLayer(trailTexture, drawCenter, rotation, drawLength, OuterTrailWidth * widthFactor, JetColor * (0.3f * fade));
                DrawTrailLayer(trailTexture, drawCenter, rotation, drawLength, InnerTrailWidth * widthFactor, HotCoreColor * (0.68f * fade));

                accumulatedLength += segmentLength;
                newerCenter = olderCenter;
            }
        }

        private static void DrawConnectorLayer(Vector2 position, float rotation, float length, float width, Color color)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Main.spriteBatch.Draw(
                pixel,
                position,
                new Rectangle(0, 0, 1, 1),
                color,
                rotation,
                new Vector2(0.5f),
                new Vector2(length, width),
                SpriteEffects.None,
                0f);
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
                SpriteEffects.FlipHorizontally,
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
                DrawCentered(QuasarJetVisualAssetSystem.HeadTexture, screenPosition, rotation, Color.White, HeadScale, SpriteEffects.FlipHorizontally);
            else
                DrawFallbackHead(screenPosition, rotation);
        }

        private static void DrawCentered(Texture2D texture, Vector2 position, float rotation, Color color, float scale, SpriteEffects effects = SpriteEffects.None)
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
                effects,
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
