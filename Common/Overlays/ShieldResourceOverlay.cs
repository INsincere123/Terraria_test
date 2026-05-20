using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Overlays
{
    /// <summary>
    /// Draws the shield overlay for vanilla horizontal resource bars.
    /// Classic/Fancy heart UI support was intentionally removed.
    /// </summary>
    [Autoload(Side = ModSide.Client)]
    public class ShieldResourceOverlay : ModResourceOverlay
    {
        private static readonly Color BarFill = new(150, 210, 255, 254);
        private static readonly Color BarBorder = new(150, 210, 255, 210);

        private const int BorderSize = 4;
        private const int BarPanelInset = 6;

        private Rectangle? _horizontalLifePanelBounds;
        private Rectangle? _horizontalLifeFillBounds;
        private int _horizontalLifeFillSegmentWidth;

        public override void PostDrawResource(ResourceOverlayDrawContext context)
        {
            if (context.DisplaySet is HorizontalBarsPlayerResourcesDisplaySet)
                CaptureHorizontalLifeBarBounds(context);
        }

        private void CaptureHorizontalLifeBarBounds(ResourceOverlayDrawContext context)
        {
            Rectangle bounds = GetContextBounds(context);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            if (IsHorizontalLifeFill(context))
            {
                _horizontalLifeFillBounds = Union(_horizontalLifeFillBounds, bounds);
                _horizontalLifeFillSegmentWidth = Math.Max(_horizontalLifeFillSegmentWidth, context.texture.Value.Width);
            }
            else
            {
                _horizontalLifePanelBounds = Union(_horizontalLifePanelBounds, bounds);
            }
        }

        public override void PostDrawResourceDisplay(
            PlayerStatsSnapshot snapshot,
            IPlayerResourcesDisplaySet displaySet,
            bool drawingLife,
            Color textColor,
            bool drawText)
        {
            if (!drawingLife)
                return;

            if (displaySet is not HorizontalBarsPlayerResourcesDisplaySet)
                return;

            Player player = Main.LocalPlayer;
            ShieldPlayer shieldPlayer = player.GetModPlayer<ShieldPlayer>();
            if (shieldPlayer.CurrentShield <= 0f)
            {
                ResetHorizontalCapture();
                return;
            }

            if (!_horizontalLifePanelBounds.HasValue)
            {
                ResetHorizontalCapture();
                return;
            }

            Rectangle panelBounds = _horizontalLifePanelBounds.Value;
            Rectangle barBounds = ResolveHorizontalLifeBarBounds(panelBounds, snapshot);
            ResetHorizontalCapture();

            float shieldRatio = MathHelper.Clamp(shieldPlayer.DisplayShield / snapshot.LifeMax, 0f, 1f);
            if (shieldRatio <= 0f)
                return;

            int x = barBounds.X;
            int right = barBounds.X + (int)MathF.Round(barBounds.Width * shieldRatio);
            int w = Math.Max(0, right - x);
            int h = barBounds.Height;
            int y = barBounds.Y;
            int b = Math.Min(BorderSize, Math.Max(1, h / 2));
            if (w < b * 2)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            for (int lineX = x + b + 1; lineX < x + w - b; lineX += 3)
                spriteBatch.Draw(pixel, new Rectangle(lineX, y + b, 1, h - b * 2), BarFill);

            spriteBatch.Draw(pixel, new Rectangle(x, y, w, b), BarBorder);
            spriteBatch.Draw(pixel, new Rectangle(x, y + h - b, w, b), BarBorder);
            spriteBatch.Draw(pixel, new Rectangle(x, y, b, h), BarBorder);
            spriteBatch.Draw(pixel, new Rectangle(x + w - b, y, b, h), BarBorder);
        }

        private Rectangle ResolveHorizontalLifeBarBounds(Rectangle panelBounds, PlayerStatsSnapshot snapshot)
        {
            if (_horizontalLifeFillBounds.HasValue)
            {
                Rectangle fillBounds = _horizontalLifeFillBounds.Value;
                int fullFillWidth = _horizontalLifeFillSegmentWidth * snapshot.AmountOfLifeHearts;
                if (fullFillWidth > 0)
                {
                    return new Rectangle(
                        fillBounds.Right - fullFillWidth,
                        fillBounds.Y,
                        fullFillWidth,
                        fillBounds.Height);
                }

                return new Rectangle(
                    panelBounds.X + BarPanelInset,
                    fillBounds.Y,
                    Math.Max(0, panelBounds.Width - BarPanelInset * 2),
                    fillBounds.Height);
            }

            return new Rectangle(
                panelBounds.X + BarPanelInset,
                panelBounds.Y + BarPanelInset,
                Math.Max(0, panelBounds.Width - BarPanelInset * 2),
                Math.Max(0, panelBounds.Height - BarPanelInset * 2));
        }

        private static Rectangle GetContextBounds(ResourceOverlayDrawContext context)
        {
            Rectangle source = context.source ?? context.texture.Frame();
            Vector2 topLeft = context.position - context.origin * context.scale;
            Vector2 size = source.Size() * context.scale;
            return new Rectangle(
                (int)MathF.Round(topLeft.X),
                (int)MathF.Round(topLeft.Y),
                (int)MathF.Round(size.X),
                (int)MathF.Round(size.Y));
        }

        private static bool IsHorizontalLifeFill(ResourceOverlayDrawContext context)
        {
            string name = context.texture.Name.Replace('\\', '/');
            return name.EndsWith("/HP_Fill") || name.EndsWith("/HP_Fill_Honey");
        }

        private static Rectangle Union(Rectangle? current, Rectangle next)
        {
            return current.HasValue ? Rectangle.Union(current.Value, next) : next;
        }

        private void ResetHorizontalCapture()
        {
            _horizontalLifePanelBounds = null;
            _horizontalLifeFillBounds = null;
            _horizontalLifeFillSegmentWidth = 0;
        }
    }
}
