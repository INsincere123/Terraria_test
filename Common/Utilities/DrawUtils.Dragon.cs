using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Content.Projectiles.Minions;

namespace TestMod.Common.Utilities
{
    public static partial class DrawUtils
    {
        private const float DragonMaxConnectorDistance = 76f;
        private const float DragonConnectorStep = 18f;
        private const float DragonConnectorScale = 0.88f;

        public static void DrawPhantasmalDragonChain(DragonSegment head, Color lightColor)
        {
            Projectile headProjectile = head.Projectile;
            // 与原绘制扫描一致：重复段索引时取最后一颗，维持/AI 则取第一颗。
            Projectile[] segments = PhantasmalDragonSummoner.GetSegments(headProjectile.owner, head.ChainId, preferLast: true);
            segments[0] ??= headProjectile;

            Texture2D texture = ModContent.Request<Texture2D>(head.Texture).Value;
            if (head.InvertColors)
                texture = PhantasmalDragonVisualAssetSystem.GetInvertedTexture(texture);

            for (int i = segments.Length - 1; i >= 0; i--)
            {
                Projectile projectile = segments[i];
                if (projectile == null)
                    continue;

                if (i > 0 && segments[i - 1] != null)
                    DrawPhantasmalDragonConnector(texture, segments[i - 1], projectile, lightColor, segments.Length);

                DrawPhantasmalDragonSprite(
                    texture,
                    projectile.Center,
                    GetPhantasmalDragonFrame(GetPhantasmalDragonSegmentKind(i, segments.Length)),
                    projectile.GetAlpha(lightColor),
                    projectile.rotation,
                    GetPhantasmalDragonSegmentScale(i, segments.Length) * projectile.scale,
                    GetPhantasmalDragonSpriteEffects(projectile));
            }
        }

        private static void DrawPhantasmalDragonConnector(Texture2D texture, Projectile previous, Projectile current, Color lightColor, int segmentCount)
        {
            Vector2 delta = current.Center - previous.Center;
            float distance = delta.Length();

            if (distance <= DragonSegment.VisualOverlapDistance || distance > DragonMaxConnectorDistance)
                return;

            int drawCount = (int)(distance / DragonConnectorStep);
            if (drawCount <= 0)
                return;

            Rectangle bodyFrame = GetPhantasmalDragonFrame(DragonSegment.BodySegmentKind);

            for (int i = 1; i <= drawCount; i++)
            {
                float completion = i / (float)(drawCount + 1);
                Vector2 position = Vector2.Lerp(previous.Center, current.Center, completion);
                float rotation = LerpAngle(previous.rotation, current.rotation, completion);
                float alpha = MathHelper.Clamp(MathF.Sin(completion * MathHelper.Pi) * 0.34f, 0f, 0.34f);
                float previousScale = GetPhantasmalDragonSegmentScale((int)previous.ai[1], segmentCount) * previous.scale;
                float currentScale = GetPhantasmalDragonSegmentScale((int)current.ai[1], segmentCount) * current.scale;
                float scale = MathHelper.Lerp(previousScale, currentScale, completion) * DragonConnectorScale;
                Color color = Color.Lerp(previous.GetAlpha(lightColor), current.GetAlpha(lightColor), completion) * alpha;

                DrawPhantasmalDragonSprite(
                    texture,
                    position,
                    bodyFrame,
                    color,
                    rotation,
                    scale,
                    GetPhantasmalDragonSpriteEffects(current));
            }
        }

        private static void DrawPhantasmalDragonSprite(Texture2D texture, Vector2 worldPosition, Rectangle frame, Color color, float rotation, float scale, SpriteEffects effects)
        {
            Main.EntitySpriteDraw(
                texture,
                worldPosition - Main.screenPosition,
                frame,
                color,
                rotation,
                frame.Size() * 0.5f,
                scale,
                effects,
                0);
        }

        private static int GetPhantasmalDragonSegmentKind(int segmentIndex, int segmentCount)
        {
            if (segmentIndex <= 0)
                return DragonSegment.HeadSegmentKind;

            if (segmentIndex >= segmentCount - 1)
                return DragonSegment.TailSegmentKind;

            return DragonSegment.BodySegmentKind;
        }

        private static float GetPhantasmalDragonSegmentScale(int segmentIndex, int segmentCount)
        {
            if (segmentIndex <= 0 || segmentIndex >= segmentCount - 1)
                return 1f;

            float bodyProgress = segmentIndex / (float)(segmentCount - 2);
            float taper = MathF.Pow(bodyProgress, 1.35f);
            return MathHelper.Lerp(1f, 0.78f, taper);
        }

        private static Rectangle GetPhantasmalDragonFrame(int segmentKind)
        {
            int row = segmentKind switch
            {
                DragonSegment.HeadSegmentKind => DragonSegment.HeadRow,
                DragonSegment.TailSegmentKind => DragonSegment.TailRow,
                _ => DragonSegment.BodyRow,
            };

            return new Rectangle(0, row * DragonSegment.FrameHeight, DragonSegment.FrameWidth, DragonSegment.FrameHeight);
        }

        private static SpriteEffects GetPhantasmalDragonSpriteEffects(Projectile projectile)
        {
            return Math.Abs(MathHelper.WrapAngle(projectile.rotation)) > MathHelper.PiOver2
                ? SpriteEffects.FlipVertically
                : SpriteEffects.None;
        }

        private static float LerpAngle(float from, float to, float completion)
        {
            return from + MathHelper.WrapAngle(to - from) * completion;
        }
    }
}
