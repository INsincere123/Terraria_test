using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TestMod.Common.DynamicText.Fonts;

namespace TestMod.Common.DynamicText
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
            DynamicTextSurface surface)
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

        private static float SmoothStep(float from, float to, float value)
        {
            if (from == to)
                return value >= to ? 1f : 0f;

            float t = MathHelper.Clamp((value - from) / (to - from), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
