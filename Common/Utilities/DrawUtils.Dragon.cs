using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using TestMod.Projectiles.Minions;

namespace TestMod.Common.Utilities
{
    public static partial class DrawUtils
    {
        private const float DragonMaxConnectorDistance = 112f;
        private const float DragonConnectorStep = 22f;
        private const float DragonConnectorScale = 0.92f;

        public static void DrawPhantasmalDragonSegment(DragonSegment segment, Color lightColor)
        {
            Projectile projectile = segment.Projectile;
            Texture2D texture = ModContent.Request<Texture2D>(segment.Texture).Value;

            if (!segment.IsHeadSegment && segment.TryGetPreviousSegment(out Projectile previous))
                DrawPhantasmalDragonConnector(texture, previous, projectile, lightColor);

            DrawPhantasmalDragonSprite(
                texture,
                projectile.Center,
                GetPhantasmalDragonFrame(segment.SegmentKind),
                lightColor,
                projectile.rotation,
                projectile.scale);
        }

        private static void DrawPhantasmalDragonConnector(Texture2D texture, Projectile previous, Projectile current, Color lightColor)
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
                float alpha = MathHelper.Clamp(MathF.Sin(completion * MathHelper.Pi) * 0.42f, 0f, 0.42f);
                float scale = MathHelper.Lerp(previous.scale, current.scale, completion) * DragonConnectorScale;

                DrawPhantasmalDragonSprite(texture, position, bodyFrame, lightColor * alpha, rotation, scale);
            }
        }

        private static void DrawPhantasmalDragonSprite(Texture2D texture, Vector2 worldPosition, Rectangle frame, Color color, float rotation, float scale)
        {
            Main.EntitySpriteDraw(
                texture,
                worldPosition - Main.screenPosition,
                frame,
                color,
                rotation,
                frame.Size() * 0.5f,
                scale,
                SpriteEffects.None,
                0);
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

        private static float LerpAngle(float from, float to, float completion)
        {
            return from + MathHelper.WrapAngle(to - from) * completion;
        }
    }
}
