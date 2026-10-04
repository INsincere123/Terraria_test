using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TestMod.Common.UI.ResourceOverlays
{
    /// <summary>透明图集分片绘制，不切换 SpriteBatch、采样器或坐标矩阵。</summary>
    internal static class ShieldBarRenderer
    {
        internal const int Width = 156;
        internal const int Height = 16;
        internal static readonly Color NormalColor = new(64, 199, 238);
        internal static readonly Color TemporaryColor = new(247, 190, 87);
        internal static readonly Color FragileColor = new(240, 162, 75);
        private static readonly Rectangle FrameSource = new(0, 0, 32, 16);
        private static readonly Rectangle TrackSource = new(0, 20, 32, 12);
        private static readonly Rectangle FillSource = new(0, 36, 32, 12);
        private static readonly Rectangle GlowSource = new(40, 0, 16, 32);
        private static Asset<Texture2D> atlas;

        internal static void Load() => atlas = ModContent.Request<Texture2D>("TestMod/Assets/Textures/UI/ShieldBarAtlas");
        internal static void Unload() => atlas = null;

        internal static void DrawStandalone(SpriteBatch batch, Rectangle bounds, ShieldBarVisualState state)
        {
            if (atlas is null || !state.Sample.Visible) return;
            Color edge = state.Sample.Fragile ? FragileColor : NormalColor;
            Rectangle inner = new(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
            DrawSkin(batch, inner, TrackSource, 3, new Color(18, 30, 45));
            DrawAmounts(batch, inner, state.Sample.Maximum, state, 1f);
            DrawSkin(batch, bounds, FrameSource, 5, edge * (0.75f + state.HitStrength * 0.25f));
            DrawEndpoint(batch, inner, state.Sample.Maximum, state, 1f);
        }

        internal static void DrawLifeOverlay(SpriteBatch batch, Rectangle bounds, int lifeMax, ShieldBarVisualState state)
        {
            if (atlas is null || !state.Sample.Visible || lifeMax <= 0) return;
            DrawAmounts(batch, bounds, lifeMax, state, 0.28f);
            int width = Pixels(bounds.Width, state.NormalDisplay + state.TemporaryDisplay, lifeMax);
            if (width > 0)
            {
                Rectangle occupied = new(bounds.X, bounds.Y, width, bounds.Height);
                Color edge = state.Sample.Fragile ? FragileColor : NormalColor;
                DrawSkin(batch, occupied, FrameSource, 5, edge * (0.65f + state.HitStrength * 0.2f));
                DrawEndpoint(batch, bounds, lifeMax, state, 0.6f);
            }
        }

        private static void DrawAmounts(SpriteBatch batch, Rectangle bounds, float maximum, ShieldBarVisualState state, float opacity)
        {
            int total = Pixels(bounds.Width, state.NormalDisplay + state.TemporaryDisplay, maximum);
            int normal = Math.Min(total, Pixels(bounds.Width, state.NormalDisplay, maximum));
            int trail = Pixels(bounds.Width, state.TrailAmount, maximum);
            if (trail > total && state.TrailOpacity > 0f)
                DrawSkin(batch, new Rectangle(bounds.X, bounds.Y, trail, bounds.Height), FillSource, 3,
                    new Color(156, 190, 205) * (state.TrailOpacity * opacity * 0.4f), bounds.X + total, bounds.X + trail);
            if (total <= 0) return;
            Rectangle occupied = new(bounds.X, bounds.Y, total, bounds.Height);
            if (normal > 0)
                DrawSkin(batch, occupied, FillSource, 3, NormalColor * opacity, bounds.X, bounds.X + normal);
            if (total > normal)
                DrawSkin(batch, occupied, FillSource, 3, TemporaryColor * opacity, bounds.X + normal, bounds.X + total);
        }

        private static int Pixels(int width, float amount, float maximum) => maximum <= 0f ? 0 :
            Math.Clamp((int)Math.Round(width * Math.Clamp(amount / maximum, 0f, 1f)), 0, width);

        private static void DrawEndpoint(SpriteBatch batch, Rectangle bounds, float maximum, ShieldBarVisualState state, float opacity)
        {
            int width = Pixels(bounds.Width, state.NormalDisplay + state.TemporaryDisplay, maximum);
            if (width <= 0) return;
            Color color = state.TemporaryDisplay > 0f ? TemporaryColor : NormalColor;
            float strength = (0.12f + state.RecoveryGlow + state.HitStrength * 0.5f) * opacity;
            batch.Draw(atlas.Value, new Rectangle(bounds.X + width - 7, bounds.Y - 5, 14, bounds.Height + 10),
                GlowSource, color * strength);
        }

        // 左右端固定，中段拉伸。裁剪仅修改源/目标矩形，蓝金交界不重新绘制圆角。
        private static void DrawSkin(SpriteBatch batch, Rectangle destination, Rectangle source, int cap, Color color,
            int clipLeft = int.MinValue, int clipRight = int.MaxValue)
        {
            if (destination.Width <= 0 || destination.Height <= 0) return;
            int edge = Math.Min(cap, destination.Width / 2);
            if (edge > 0)
            {
                DrawPiece(batch, new Rectangle(destination.X, destination.Y, edge, destination.Height),
                    new Rectangle(source.X, source.Y, cap, source.Height), color, clipLeft, clipRight);
                DrawPiece(batch, new Rectangle(destination.Right - edge, destination.Y, edge, destination.Height),
                    new Rectangle(source.Right - cap, source.Y, cap, source.Height), color, clipLeft, clipRight);
            }
            DrawPiece(batch, new Rectangle(destination.X + edge, destination.Y, destination.Width - edge * 2, destination.Height),
                new Rectangle(source.X + cap, source.Y, source.Width - cap * 2, source.Height), color, clipLeft, clipRight);
        }

        private static void DrawPiece(SpriteBatch batch, Rectangle destination, Rectangle source, Color color, int left, int right)
        {
            if (destination.Width <= 0) return;
            int x = Math.Max(destination.X, left);
            int end = Math.Min(destination.Right, right);
            if (end <= x) return;
            int sourceLeft = source.X + (int)((x - destination.X) * (source.Width / (float)destination.Width));
            int sourceRight = source.X + (int)Math.Ceiling((end - destination.X) * (source.Width / (float)destination.Width));
            sourceRight = Math.Min(source.Right, Math.Max(sourceLeft + 1, sourceRight));
            batch.Draw(atlas.Value, new Rectangle(x, destination.Y, end - x, destination.Height),
                new Rectangle(sourceLeft, source.Y, sourceRight - sourceLeft, source.Height), color);
        }

        internal static void DrawCenteredText(SpriteBatch batch, string text, Vector2 topCenter, Color color, float scale)
        {
            float textWidth = FontAssets.MouseText.Value.MeasureString(text).X;
            if (textWidth > 0f) scale = Math.Min(scale, Width / textWidth);
            Utils.DrawBorderString(batch, text, topCenter, color, scale, 0.5f);
        }
    }
}
