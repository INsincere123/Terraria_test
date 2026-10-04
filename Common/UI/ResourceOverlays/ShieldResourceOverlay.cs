using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.UI.ResourceOverlays
{
    /// <summary>
    /// Draws the shield overlay for vanilla horizontal resource bars.
    /// Classic/Fancy heart UI support was intentionally removed.
    /// </summary>
    [Autoload(Side = ModSide.Client)]
    public class ShieldResourceOverlay : ModResourceOverlay
    {
        private const int BarPanelInset = 6;

        private Rectangle? _horizontalLifePanelBounds;
        private Rectangle? _horizontalLifeFillBounds;
        private int _horizontalLifeFillSegmentWidth;

        private bool capturingLife;

        public override bool PreDrawResourceDisplay(PlayerStatsSnapshot snapshot,
            IPlayerResourcesDisplaySet displaySet, bool drawingLife, ref Color textColor, out bool drawText)
        {
            capturingLife = drawingLife;
            if (drawingLife)
            {
                ResetHorizontalCapture();
                ShieldBarVisualSystem.LifeBarBounds = null;
            }
            drawText = true;
            return true;
        }

        public override void PostDrawResource(ResourceOverlayDrawContext context)
        {
            if (capturingLife && context.DisplaySet is HorizontalBarsPlayerResourcesDisplaySet)
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

            if (!_horizontalLifePanelBounds.HasValue)
            {
                ResetHorizontalCapture();
                return;
            }
            Rectangle panelBounds = _horizontalLifePanelBounds.Value;
            Rectangle barBounds = ResolveHorizontalLifeBarBounds(panelBounds, snapshot);
            ShieldBarVisualSystem.LifeBarBounds = barBounds;
            ResetHorizontalCapture();
            // 即使护盾为空也捕获位置；独立条仍须显示等待倒计时。
            if (!Main.LocalPlayer.dead)
                ShieldBarRenderer.DrawLifeOverlay(Main.spriteBatch, barBounds, snapshot.LifeMax,
                    ShieldBarVisualSystem.State);
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
