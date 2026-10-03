using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using TestMod.Common.Graphics.DynamicText.Fonts;

namespace TestMod.Common.Graphics.DynamicText
{
    public enum DynamicTextSurface
    {
        Tooltip,
        World
    }

    public readonly struct DynamicTextDrawContext
    {
        public readonly SpriteBatch SpriteBatch;
        public readonly DynamicTextFont Font;
        public readonly string Text;
        public readonly Vector2 Position;
        public readonly float Rotation;
        public readonly Vector2 Origin;
        public readonly Vector2 Scale;
        public readonly float Time;
        public readonly float Progress;
        public readonly float Opacity;
        public readonly int Seed;
        public readonly float MaxWidth;
        public readonly float Spread;
        public readonly Color PrimaryColor;
        public readonly Color SecondaryColor;
        public readonly Color ShadowColor;
        public readonly DynamicTextSurface Surface;
        public readonly DynamicTextLayout Layout;

        public Vector2 TextSize => Layout.Size * Scale;

        // local 是已缩放的文字局部坐标；所有装饰共用正文的原点和旋转。
        public Vector2 ToScreen(Vector2 local) => Position + (local - Origin * Scale).RotatedBy(Rotation);

        public void DrawText(Vector2 position, Color color, bool ignoreColors = false) =>
            Font.DrawLayout(SpriteBatch, Layout, position, color, Rotation, Origin, Scale, MaxWidth, ignoreColors);

        public void DrawShadow(Color color)
        {
            if (Spread <= 0f)
                return;
            DrawText(Position + new Vector2(-Spread, 0f), color, true);
            DrawText(Position + new Vector2(Spread, 0f), color, true);
            DrawText(Position + new Vector2(0f, -Spread), color, true);
            DrawText(Position + new Vector2(0f, Spread), color, true);
        }

        public void DrawGlyph(string glyph, Vector2 localOffset, Color color) =>
            Font.DrawGlyph(SpriteBatch, glyph, Position + localOffset.RotatedBy(Rotation), color, Rotation, Origin, Scale);

        public DynamicTextDrawContext(
            SpriteBatch spriteBatch,
            DynamicTextFont font,
            string text,
            Vector2 position,
            float rotation,
            Vector2 origin,
            Vector2 scale,
            float time,
            float progress,
            float opacity,
            int seed,
            float maxWidth,
            float spread,
            Color primaryColor,
            Color secondaryColor,
            Color shadowColor,
            DynamicTextSurface surface,
            DynamicTextLayout layout = null)
        {
            SpriteBatch = spriteBatch;
            Font = font;
            Text = text;
            Position = position;
            Rotation = rotation;
            Origin = origin;
            Scale = scale;
            Time = time;
            Progress = progress;
            Opacity = opacity;
            Seed = seed;
            MaxWidth = maxWidth;
            Spread = spread;
            PrimaryColor = primaryColor;
            SecondaryColor = secondaryColor;
            ShadowColor = shadowColor;
            Surface = surface;
            Layout = layout ?? DynamicTextLayout.Get(font, text, scale.X > 0f && maxWidth > 0f ? maxWidth / scale.X : -1f);
        }
    }

    public interface IDynamicTextLayer
    {
        void Draw(in DynamicTextDrawContext context);
    }

    public sealed class DynamicTextStyle
    {
        public string Key { get; }
        public Color PrimaryColor { get; init; } = Color.White;
        public Color SecondaryColor { get; init; } = Color.White;
        public Color ShadowColor { get; init; } = Color.Black;
        public int Lifetime { get; init; } = 72;
        public Vector2 Velocity { get; init; } = new(0f, -1.6f);
        public Vector2? RandomVelocityMin { get; init; }
        public Vector2? RandomVelocityMax { get; init; }
        public float Gravity { get; init; } = 0.018f;
        public float Drag { get; init; } = 0.985f;
        public float BaseScale { get; init; } = 1f;
        public float EndScale { get; init; } = 0.86f;
        public float CritScaleBonus { get; init; } = 0.24f;
        public DynamicTextFontSpec FontSpec { get; init; } = DynamicTextFontSpec.Default;
        public IReadOnlyList<IDynamicTextLayer> Layers { get; init; } = [];

        public DynamicTextStyle(string key)
        {
            Key = key;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            foreach (IDynamicTextLayer layer in Layers)
                layer.Draw(context);
        }

        public float GetWorldOpacity(float progress)
        {
            float fadeIn = SmoothStep(0f, 0.12f, progress);
            float fadeOut = 1f - SmoothStep(0.72f, 1f, progress);
            return MathHelper.Clamp(fadeIn * fadeOut, 0f, 1f);
        }

        public float GetWorldScale(float progress, bool crit, float requestScale)
        {
            float pop = MathF.Sin(MathHelper.Clamp(progress / 0.18f, 0f, 1f) * MathHelper.Pi);
            float settle = MathHelper.Lerp(1f, EndScale, MathF.Pow(progress, 1.35f));
            float critBonus = crit ? CritScaleBonus : 0f;
            return requestScale * BaseScale * settle * (1f + pop * (0.18f + critBonus));
        }

        public Vector2 GetInitialVelocity()
        {
            if (RandomVelocityMin.HasValue && RandomVelocityMax.HasValue)
            {
                Vector2 min = RandomVelocityMin.Value;
                Vector2 max = RandomVelocityMax.Value;
                return new Vector2(
                    Main.rand.NextFloat(min.X, max.X),
                    Main.rand.NextFloat(min.Y, max.Y));
            }

            return Velocity;
        }

        private static float SmoothStep(float from, float to, float value)
        {
            if (from == to)
                return value >= to ? 1f : 0f;

            float t = MathHelper.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
