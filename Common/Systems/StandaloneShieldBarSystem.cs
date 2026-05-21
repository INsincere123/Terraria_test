using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using TestMod.Common.Configs;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    [Autoload(Side = ModSide.Client)]
    public class StandaloneShieldBarSystem : ModSystem
    {
        private const int BarWidth = 92;
        private const int BarHeight = 14;
        private const int Border = 2;
        private const int CornerRadius = 5;
        private const int GapFromLifeBar = 12;

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (Main.dedServ)
                return;

            TestModClientConfig config = TestModClientConfig.Instance;
            if (config is null || !config.ShowStandaloneShieldBar)
                return;

            Player player = Main.LocalPlayer;
            if (player is null || !player.active)
                return;

            EnergyShieldPlayer shieldPlayer = player.GetModPlayer<EnergyShieldPlayer>();
            if (shieldPlayer.MaxShield <= 0f)
                return;

            float shieldRatio = MathHelper.Clamp(shieldPlayer.DisplayShield / shieldPlayer.MaxShield, 0f, 1f);
            Vector2 position = GetDefaultPosition() + new Vector2(
                config.StandaloneShieldBarOffsetX,
                config.StandaloneShieldBarOffsetY);

            DrawShieldBar(spriteBatch, position, shieldRatio, shieldPlayer);
        }

        private static Vector2 GetDefaultPosition()
        {
            // Match the vanilla HorizontalBars life anchor closely, then place this bar to its left.
            int lifeBarX = Main.screenWidth - 300 - 22 + 16;
            int lifeBarY = 18 + 6;
            return new Vector2(lifeBarX - BarWidth - GapFromLifeBar, lifeBarY);
        }

        private static void DrawShieldBar(SpriteBatch spriteBatch, Vector2 position, float shieldRatio, EnergyShieldPlayer shieldPlayer)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int x = (int)MathF.Round(position.X);
            int y = (int)MathF.Round(position.Y);

            Rectangle outer = new(x, y, BarWidth, BarHeight);
            Rectangle inner = new(x + Border, y + Border, BarWidth - Border * 2, BarHeight - Border * 2);
            int fillWidth = (int)MathF.Round(inner.Width * shieldRatio);
            Rectangle fill = new(inner.X, inner.Y, fillWidth, inner.Height);

            Color shieldColor = new Color(39, 211, 245);
            Color edgeColor = new Color(0, 210, 255);

            DrawRoundedRectangle(spriteBatch, pixel, outer, CornerRadius, edgeColor * 0.96f);
            DrawRoundedRectangle(spriteBatch, pixel, new Rectangle(outer.X + 1, outer.Y + 1, outer.Width - 2, outer.Height - 2), CornerRadius - 1, new Color(3, 10, 24, 220));

            DrawRoundedRectangle(spriteBatch, pixel, inner, CornerRadius - Border, shieldColor * 0.25f);
            if (fill.Width <= 0)
                return;

            int fillRadius = Math.Min(CornerRadius - Border, Math.Max(1, fill.Width / 2));
            DrawRoundedRectangle(spriteBatch, pixel, fill, fillRadius, shieldColor * 0.96f);
            DrawRoundedRectangle(spriteBatch, pixel, new Rectangle(fill.X, fill.Y, fill.Width, Math.Max(1, fill.Height / 3)), Math.Min(fillRadius, 2), edgeColor * 0.78f);
            spriteBatch.Draw(pixel, new Rectangle(fill.Right - 1, fill.Y, 1, fill.Height), Color.White * 0.78f);
        }

        private static void DrawRoundedRectangle(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int radius, Color color)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            radius = Math.Clamp(radius, 0, Math.Min(rect.Width, rect.Height) / 2);
            if (radius <= 0)
            {
                spriteBatch.Draw(pixel, rect, color);
                return;
            }

            for (int row = 0; row < rect.Height; row++)
            {
                int leftInset = 0;
                int rightInset = 0;

                if (row < radius)
                {
                    float dy = radius - row - 0.5f;
                    leftInset = radius - (int)MathF.Sqrt(Math.Max(0f, radius * radius - dy * dy));
                    rightInset = leftInset;
                }
                else if (row >= rect.Height - radius)
                {
                    float dy = row - (rect.Height - radius) + 0.5f;
                    leftInset = radius - (int)MathF.Sqrt(Math.Max(0f, radius * radius - dy * dy));
                    rightInset = leftInset;
                }

                int lineX = rect.X + leftInset;
                int lineWidth = rect.Width - leftInset - rightInset;
                if (lineWidth > 0)
                    spriteBatch.Draw(pixel, new Rectangle(lineX, rect.Y + row, lineWidth, 1), color);
            }
        }
    }
}
