using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace TestMod.Common.Utilities
{
    public static partial class DrawUtils
    {
        public static void DrawMomentumChargeBar(SpriteBatch spriteBatch, Player player, int charges, float progress)
        {
            const int segmentWidth = 19;
            const int gap = 3;
            const int totalWidth = segmentWidth * 2 + gap;
            Vector2 foot = player.gravDir < 0f ? player.Top : player.Bottom;
            foot.Y += player.gravDir * 12f;
            // Game 接口层只提供 ZoomMatrix；先补世界的翻转矩阵，不重复缩放。
            Vector2 position = Vector2.Transform(foot - Main.screenPosition, Main.GameViewMatrix.EffectMatrix);
            position -= new Vector2(totalWidth * 0.5f, 3f);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            for (int i = 0; i < 2; i++)
            {
                Rectangle border = new((int)MathF.Round(position.X) + i * (segmentWidth + gap), (int)MathF.Round(position.Y), segmentWidth, 6);
                spriteBatch.Draw(pixel, border, new Color(18, 24, 38, 220));
                Rectangle inner = new(border.X + 1, border.Y + 1, border.Width - 2, border.Height - 2);
                spriteBatch.Draw(pixel, inner, new Color(47, 53, 72, 220));
                float fill = i < charges ? 1f : i == charges ? Math.Clamp(progress, 0f, 1f) : 0f;
                inner.Width = (int)MathF.Round(inner.Width * fill);
                if (inner.Width <= 0)
                    continue;
                Color tint = i == 0 ? new Color(70, 211, 255) : new Color(175, 100, 255);
                spriteBatch.Draw(pixel, inner, tint * (i < charges ? 1f : 0.65f));
                spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), Color.White * 0.5f);
            }
        }
    }
}
