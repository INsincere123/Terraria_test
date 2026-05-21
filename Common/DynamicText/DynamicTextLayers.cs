using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace TestMod.Common.DynamicText
{
    public sealed class TextShadowLayer : IDynamicTextLayer
    {
        private readonly float opacity;
        private readonly bool drawMain;

        public TextShadowLayer(float opacity = 1f, bool drawMain = true)
        {
            this.opacity = opacity;
            this.drawMain = drawMain;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            Color color = context.PrimaryColor * (context.Opacity * opacity);
            if (drawMain)
            {
                ChatManager.DrawColorCodedStringWithShadow(
                    context.SpriteBatch,
                    context.Font,
                    context.Text,
                    context.Position,
                    color,
                    context.Rotation,
                    context.Origin,
                    context.Scale,
                    context.MaxWidth,
                    context.Spread);
                return;
            }

            ChatManager.DrawColorCodedString(
                context.SpriteBatch,
                context.Font,
                context.Text,
                context.Position,
                color,
                context.Rotation,
                context.Origin,
                context.Scale);
        }
    }

    public sealed class TextOutlineLayer : IDynamicTextLayer
    {
        private readonly float radius;
        private readonly float outlineOpacity;
        private readonly float fillOpacity;

        public TextOutlineLayer(float radius = 1.8f, float outlineOpacity = 1f, float fillOpacity = 1f)
        {
            this.radius = radius;
            this.outlineOpacity = outlineOpacity;
            this.fillOpacity = fillOpacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (context.Opacity <= 0f)
                return;

            Color outline = context.ShadowColor * (outlineOpacity * context.Opacity);
            Color fill = context.PrimaryColor * (fillOpacity * context.Opacity);

            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 offset = new(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
                ChatManager.DrawColorCodedString(
                    context.SpriteBatch,
                    context.Font,
                    context.Text,
                    context.Position + offset,
                    outline,
                    context.Rotation,
                    context.Origin,
                    context.Scale);
            }

            ChatManager.DrawColorCodedString(
                context.SpriteBatch,
                context.Font,
                context.Text,
                context.Position,
                fill,
                context.Rotation,
                context.Origin,
                context.Scale);
        }
    }

    public sealed class TextGlowLayer : IDynamicTextLayer
    {
        private readonly int layers;
        private readonly float radius;
        private readonly float opacity;
        private readonly float pulseSpeed;
        private readonly float verticalSquash;

        public TextGlowLayer(int layers, float radius, float opacity, float pulseSpeed = 2.4f, float verticalSquash = 0.55f)
        {
            this.layers = layers;
            this.radius = radius;
            this.opacity = opacity;
            this.pulseSpeed = pulseSpeed;
            this.verticalSquash = verticalSquash;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (layers <= 0 || context.Opacity <= 0f)
                return;

            float colorPulse = 0.5f + 0.5f * MathF.Sin(context.Time * pulseSpeed + context.Seed * 0.17f);
            Color color = Color.Lerp(context.PrimaryColor, context.SecondaryColor, colorPulse) * (opacity * context.Opacity);
            float pulseRadius = radius * (0.86f + 0.18f * MathF.Sin(context.Time * pulseSpeed * 1.31f));

            for (int i = 0; i < layers; i++)
            {
                float angle = MathHelper.TwoPi * i / layers + context.Time * 0.8f;
                Vector2 offset = new(MathF.Cos(angle) * pulseRadius, MathF.Sin(angle) * pulseRadius * verticalSquash);
                ChatManager.DrawColorCodedString(
                    context.SpriteBatch,
                    context.Font,
                    context.Text,
                    context.Position + offset,
                    color,
                    context.Rotation,
                    context.Origin,
                    context.Scale);
            }
        }
    }

    public sealed class TextSweepLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly float speed;
        private readonly float width;
        private readonly float interval;
        private readonly float opacity;

        public TextSweepLayer(Color color, float speed, float width, float interval, float opacity = 1f)
        {
            this.color = color;
            this.speed = speed;
            this.width = width;
            this.interval = interval;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (string.IsNullOrEmpty(context.Text) || width <= 0f || context.Opacity <= 0f)
                return;

            float textWidth = context.Font.MeasureString(context.Text).X * context.Scale.X;
            float cycle = textWidth + width * 2f + Math.Abs(speed) * interval;
            float shineX = PositiveModulo(context.Time * speed + context.Seed * 13f, cycle) - width;
            float charOffset = 0f;

            foreach (char character in context.Text)
            {
                string glyph = character.ToString();
                float glyphWidth = context.Font.MeasureString(glyph).X * context.Scale.X;
                float center = charOffset + glyphWidth * 0.5f;
                float intensity = 1f - MathHelper.Clamp(Math.Abs(center - shineX) / width, 0f, 1f);
                intensity *= intensity;

                if (intensity > 0.01f)
                {
                    ChatManager.DrawColorCodedString(
                        context.SpriteBatch,
                        context.Font,
                        glyph,
                        context.Position + Vector2.UnitX * charOffset,
                        color * (intensity * opacity * context.Opacity),
                        context.Rotation,
                        context.Origin,
                        context.Scale);
                }

                charOffset += glyphWidth;
            }
        }

        private static float PositiveModulo(float value, float modulus)
        {
            float result = value % modulus;
            return result < 0f ? result + modulus : result;
        }
    }

    public sealed class TextChromaticLayer : IDynamicTextLayer
    {
        private readonly float offset;
        private readonly float opacity;
        private readonly float wobbleSpeed;

        public TextChromaticLayer(float offset, float opacity, float wobbleSpeed = 2.2f)
        {
            this.offset = offset;
            this.opacity = opacity;
            this.wobbleSpeed = wobbleSpeed;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (offset <= 0f || context.Opacity <= 0f)
                return;

            float pulse = 0.75f + 0.25f * MathF.Sin(context.Time * wobbleSpeed + context.Seed * 0.03f);
            Vector2 axis = new Vector2(1f, 0.18f).SafeNormalize(Vector2.UnitX) * offset * pulse;
            DrawShift(context, -axis, new Color(255, 40, 44), 0.8f);
            DrawShift(context, axis, new Color(40, 225, 255), 0.7f);
            DrawShift(context, axis.RotatedBy(MathHelper.PiOver2) * 0.45f, new Color(210, 80, 255), 0.45f);
        }

        private void DrawShift(in DynamicTextDrawContext context, Vector2 offsetVector, Color color, float weight)
        {
            ChatManager.DrawColorCodedString(
                context.SpriteBatch,
                context.Font,
                context.Text,
                context.Position + offsetVector,
                color * (opacity * weight * context.Opacity),
                context.Rotation,
                context.Origin,
                context.Scale);
        }
    }

    public sealed class TextGlyphWaveLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly Vector2 direction;
        private readonly float amplitude;
        private readonly float phaseStep;
        private readonly float speed;
        private readonly float opacity;

        public TextGlyphWaveLayer(Color color, Vector2 direction, float amplitude, float phaseStep, float speed, float opacity)
        {
            this.color = color;
            this.direction = direction.SafeNormalize(Vector2.UnitY);
            this.amplitude = amplitude;
            this.phaseStep = phaseStep;
            this.speed = speed;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (string.IsNullOrEmpty(context.Text) || amplitude == 0f || context.Opacity <= 0f)
                return;

            float charOffset = 0f;
            for (int i = 0; i < context.Text.Length; i++)
            {
                string glyph = context.Text[i].ToString();
                float glyphWidth = context.Font.MeasureString(glyph).X * context.Scale.X;
                float wave = MathF.Sin(context.Time * speed + i * phaseStep + context.Seed * 0.01f);
                Vector2 offset = direction * amplitude * wave;

                ChatManager.DrawColorCodedString(
                    context.SpriteBatch,
                    context.Font,
                    glyph,
                    context.Position + Vector2.UnitX * charOffset + offset,
                    color * (opacity * context.Opacity * (0.72f + 0.28f * MathF.Abs(wave))),
                    context.Rotation,
                    context.Origin,
                    context.Scale);

                charOffset += glyphWidth;
            }
        }
    }

    public sealed class TextTracerLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly int tracerCount;
        private readonly float speed;
        private readonly float length;
        private readonly float width;
        private readonly float opacity;

        public TextTracerLayer(Color color, int tracerCount, float speed, float length, float width, float opacity)
        {
            this.color = color;
            this.tracerCount = tracerCount;
            this.speed = speed;
            this.length = length;
            this.width = width;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (tracerCount <= 0 || context.Opacity <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            Vector2 size = context.Font.MeasureString(context.Text) * context.Scale;
            if (size.X <= 1f || size.Y <= 1f)
                return;

            float cycle = size.X + length * 2f + Math.Abs(speed) * 0.8f;
            for (int i = 0; i < tracerCount; i++)
            {
                float seed = Hash01(context.Seed + i * 83);
                float x = PositiveModulo(context.Time * speed + seed * cycle, cycle) - length;
                float y = size.Y * (0.22f + Hash01(context.Seed + i * 193) * 0.56f);
                float centerFade = 1f - MathHelper.Clamp(Math.Abs(x - size.X * 0.5f) / (size.X * 0.72f), 0f, 1f);
                float alpha = (0.28f + centerFade * 0.72f) * opacity * context.Opacity;
                Vector2 position = context.Position + new Vector2(x, y);

                context.SpriteBatch.Draw(
                    pixel,
                    position,
                    source,
                    color * alpha,
                    0f,
                    new Vector2(0f, 0.5f),
                    new Vector2(length, width),
                    SpriteEffects.None,
                    0f);
            }
        }

        private static float PositiveModulo(float value, float modulus)
        {
            float result = value % modulus;
            return result < 0f ? result + modulus : result;
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                uint x = (uint)seed;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00FFFFFF) / 16777215f;
            }
        }
    }

    public sealed class TextArcaneOrbitLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly int runeCount;
        private readonly float radiusPadding;
        private readonly float speed;
        private readonly float runeSize;
        private readonly float opacity;

        public TextArcaneOrbitLayer(Color color, int runeCount, float radiusPadding, float speed, float runeSize, float opacity)
        {
            this.color = color;
            this.runeCount = runeCount;
            this.radiusPadding = radiusPadding;
            this.speed = speed;
            this.runeSize = runeSize;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (runeCount <= 0 || context.Opacity <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 size = context.Font.MeasureString(context.Text) * context.Scale;
            if (size.X <= 1f || size.Y <= 1f)
                return;

            Vector2 topLeft = context.Position - context.Origin * context.Scale;
            Vector2 center = topLeft + size * 0.5f;
            Vector2 ellipse = size * 0.5f + new Vector2(radiusPadding * context.Scale.X, radiusPadding * 0.55f * context.Scale.Y);
            Rectangle source = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f);

            for (int i = 0; i < runeCount; i++)
            {
                float seed = Hash01(context.Seed + i * 197);
                float angle = MathHelper.TwoPi * (i / (float)runeCount) + context.Time * speed * (0.72f + seed * 0.56f);
                Vector2 offset = new(MathF.Cos(angle) * ellipse.X, MathF.Sin(angle) * ellipse.Y);
                float depth = 0.58f + 0.42f * MathF.Sin(angle + context.Time * 0.7f);
                float twinkle = MathF.Pow(0.5f + 0.5f * MathF.Sin(context.Time * 5.1f + seed * MathHelper.TwoPi), 2.3f);
                Color runeColor = Color.Lerp(context.SecondaryColor, color, depth) * ((0.32f + twinkle * 0.68f) * opacity * context.Opacity);
                float scale = runeSize * context.Scale.X * (0.68f + depth * 0.32f);
                Vector2 position = center + offset;

                DrawDiamond(context.SpriteBatch, pixel, source, position, runeColor, angle, origin, scale);
                DrawSpark(context.SpriteBatch, pixel, source, position, runeColor * 0.55f, angle, origin, scale * 1.25f);
            }
        }

        private static void DrawDiamond(SpriteBatch spriteBatch, Texture2D pixel, Rectangle source, Vector2 position, Color color, float angle, Vector2 origin, float scale)
        {
            spriteBatch.Draw(pixel, position, source, color, angle + MathHelper.PiOver4, origin, new Vector2(scale, scale), SpriteEffects.None, 0f);
        }

        private static void DrawSpark(SpriteBatch spriteBatch, Texture2D pixel, Rectangle source, Vector2 position, Color color, float angle, Vector2 origin, float scale)
        {
            spriteBatch.Draw(pixel, position, source, color, angle, origin, new Vector2(scale * 1.6f, 1f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, position, source, color, angle + MathHelper.PiOver2, origin, new Vector2(scale * 1.6f, 1f), SpriteEffects.None, 0f);
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                uint x = (uint)seed;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00FFFFFF) / 16777215f;
            }
        }
    }

    public sealed class TextManaWispLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly int wispCount;
        private readonly float height;
        private readonly float speed;
        private readonly float width;
        private readonly float opacity;

        public TextManaWispLayer(Color color, int wispCount, float height, float speed, float width, float opacity)
        {
            this.color = color;
            this.wispCount = wispCount;
            this.height = height;
            this.speed = speed;
            this.width = width;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (wispCount <= 0 || context.Opacity <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 size = context.Font.MeasureString(context.Text) * context.Scale;
            if (size.X <= 1f || size.Y <= 1f)
                return;

            Vector2 topLeft = context.Position - context.Origin * context.Scale;
            Rectangle source = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f, 1f);
            float scaledHeight = height * context.Scale.Y;

            for (int i = 0; i < wispCount; i++)
            {
                float seed = Hash01(context.Seed + i * 269);
                float cycle = PositiveModulo(context.Time * speed + seed, 1f);
                float x = topLeft.X + size.X * Hash01(context.Seed + i * 491);
                float y = topLeft.Y + size.Y * (0.92f - cycle * 0.86f);
                float sway = MathF.Sin(context.Time * 4.3f + i * 1.67f) * 4.2f * context.Scale.X;
                float fade = MathF.Sin(cycle * MathHelper.Pi);
                float length = scaledHeight * (0.35f + cycle * 0.65f);
                Color wispColor = Color.Lerp(context.PrimaryColor, color, cycle) * (fade * opacity * context.Opacity);

                context.SpriteBatch.Draw(
                    pixel,
                    new Vector2(x + sway, y),
                    source,
                    wispColor,
                    -0.18f + sway * 0.015f,
                    origin,
                    new Vector2(width * context.Scale.X, length),
                    SpriteEffects.None,
                    0f);
            }
        }

        private static float PositiveModulo(float value, float modulus)
        {
            float result = value % modulus;
            return result < 0f ? result + modulus : result;
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                uint x = (uint)seed;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00FFFFFF) / 16777215f;
            }
        }
    }

    public sealed class TextSlashLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly int slashCount;
        private readonly float speed;
        private readonly float length;
        private readonly float width;
        private readonly float opacity;

        public TextSlashLayer(Color color, int slashCount, float speed, float length, float width, float opacity)
        {
            this.color = color;
            this.slashCount = slashCount;
            this.speed = speed;
            this.length = length;
            this.width = width;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (slashCount <= 0 || context.Opacity <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            Vector2 size = context.Font.MeasureString(context.Text) * context.Scale;
            if (size.X <= 1f || size.Y <= 1f)
                return;

            Vector2 topLeft = context.Position - context.Origin * context.Scale;
            float cycle = size.X + length * 2f;
            for (int i = 0; i < slashCount; i++)
            {
                float seed = Hash01(context.Seed + i * 157);
                float sweep = PositiveModulo(context.Time * speed + seed * cycle, cycle) - length;
                float y = size.Y * (0.24f + Hash01(context.Seed + i * 313) * 0.52f);
                float fade = 1f - MathHelper.Clamp(Math.Abs(sweep - size.X * 0.5f) / (size.X * 0.65f), 0f, 1f);
                fade *= fade;
                if (fade <= 0.02f)
                    continue;

                Vector2 position = topLeft + new Vector2(sweep, y);
                context.SpriteBatch.Draw(
                    pixel,
                    position,
                    source,
                    color * (fade * opacity * context.Opacity),
                    -0.48f,
                    new Vector2(0f, 0.5f),
                    new Vector2(length, width) * context.Scale.X,
                    SpriteEffects.None,
                    0f);
            }
        }

        private static float PositiveModulo(float value, float modulus)
        {
            float result = value % modulus;
            return result < 0f ? result + modulus : result;
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                uint x = (uint)seed;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00FFFFFF) / 16777215f;
            }
        }
    }

    public sealed class TextReticleLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly float bracketLength;
        private readonly float inset;
        private readonly float width;
        private readonly float opacity;

        public TextReticleLayer(Color color, float bracketLength, float inset, float width, float opacity)
        {
            this.color = color;
            this.bracketLength = bracketLength;
            this.inset = inset;
            this.width = width;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (context.Opacity <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            Vector2 size = context.Font.MeasureString(context.Text) * context.Scale;
            if (size.X <= 1f || size.Y <= 1f)
                return;

            Vector2 topLeft = context.Position - context.Origin * context.Scale;
            float pulse = 0.78f + 0.22f * MathF.Sin(context.Time * 5.4f + context.Seed * 0.03f);
            Color drawColor = color * (opacity * pulse * context.Opacity);
            float lineWidth = width * context.Scale.X;
            float lineLength = bracketLength * context.Scale.X;
            float pad = inset * context.Scale.X;

            DrawCorner(context.SpriteBatch, pixel, source, topLeft + new Vector2(-pad, -pad), lineLength, lineWidth, drawColor, 1f, 1f);
            DrawCorner(context.SpriteBatch, pixel, source, topLeft + new Vector2(size.X + pad, -pad), lineLength, lineWidth, drawColor, -1f, 1f);
            DrawCorner(context.SpriteBatch, pixel, source, topLeft + new Vector2(-pad, size.Y + pad), lineLength, lineWidth, drawColor, 1f, -1f);
            DrawCorner(context.SpriteBatch, pixel, source, topLeft + size + new Vector2(pad), lineLength, lineWidth, drawColor, -1f, -1f);
        }

        private static void DrawCorner(SpriteBatch spriteBatch, Texture2D pixel, Rectangle source, Vector2 corner, float length, float width, Color color, float xDirection, float yDirection)
        {
            spriteBatch.Draw(pixel, corner, source, color, 0f, new Vector2(0f, 0.5f), new Vector2(length * xDirection, width), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, corner, source, color, MathHelper.PiOver2, new Vector2(0f, 0.5f), new Vector2(length * yDirection, width), SpriteEffects.None, 0f);
        }
    }

    public sealed class TextSoulWispLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly int wispCount;
        private readonly float orbitRadius;
        private readonly float speed;
        private readonly float size;
        private readonly float opacity;

        public TextSoulWispLayer(Color color, int wispCount, float orbitRadius, float speed, float size, float opacity)
        {
            this.color = color;
            this.wispCount = wispCount;
            this.orbitRadius = orbitRadius;
            this.speed = speed;
            this.size = size;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (wispCount <= 0 || context.Opacity <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            Vector2 textSize = context.Font.MeasureString(context.Text) * context.Scale;
            Vector2 topLeft = context.Position - context.Origin * context.Scale;
            Vector2 center = topLeft + textSize * 0.5f;
            Vector2 origin = new(0.5f);

            for (int i = 0; i < wispCount; i++)
            {
                float seed = Hash01(context.Seed + i * 227);
                float phase = context.Time * speed * (0.75f + seed * 0.5f) + MathHelper.TwoPi * i / wispCount;
                Vector2 orbit = new(MathF.Cos(phase) * (textSize.X * 0.48f + orbitRadius), MathF.Sin(phase * 1.37f) * (textSize.Y * 0.42f + orbitRadius * 0.42f));
                float pulse = 0.55f + 0.45f * MathF.Sin(context.Time * 4.7f + seed * MathHelper.TwoPi);
                Color wispColor = Color.Lerp(context.PrimaryColor, color, pulse) * (opacity * (0.55f + pulse * 0.45f) * context.Opacity);
                Vector2 position = center + orbit + Vector2.UnitY * MathF.Sin(context.Time * 2.2f + i) * 2.5f;
                float scale = size * context.Scale.X * (0.75f + pulse * 0.5f);

                DrawSoftDot(context.SpriteBatch, pixel, source, position, wispColor * 0.42f, origin, scale * 2.6f);
                DrawSoftDot(context.SpriteBatch, pixel, source, position, wispColor, origin, scale);
            }
        }

        private static void DrawSoftDot(SpriteBatch spriteBatch, Texture2D pixel, Rectangle source, Vector2 position, Color color, Vector2 origin, float scale)
        {
            spriteBatch.Draw(pixel, position, source, color, 0f, origin, new Vector2(scale), SpriteEffects.None, 0f);
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                uint x = (uint)seed;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00FFFFFF) / 16777215f;
            }
        }
    }

    public sealed class TextRuptureLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly int crackCount;
        private readonly float length;
        private readonly float width;
        private readonly float opacity;

        public TextRuptureLayer(Color color, int crackCount, float length, float width, float opacity)
        {
            this.color = color;
            this.crackCount = crackCount;
            this.length = length;
            this.width = width;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (crackCount <= 0 || context.Opacity <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            Vector2 size = context.Font.MeasureString(context.Text) * context.Scale;
            if (size.X <= 1f || size.Y <= 1f)
                return;

            Vector2 topLeft = context.Position - context.Origin * context.Scale;
            for (int i = 0; i < crackCount; i++)
            {
                float seed = Hash01(context.Seed + i * 347);
                float x = size.X * seed;
                float y = size.Y * (0.18f + Hash01(context.Seed + i * 541) * 0.64f);
                float flicker = 0.65f + 0.35f * MathF.Sin(context.Time * 8.5f + i * 1.4f);
                Vector2 position = topLeft + new Vector2(x, y);
                float crackLength = length * context.Scale.Y * (0.65f + Hash01(context.Seed + i * 719) * 0.65f);
                float angle = -0.95f + Hash01(context.Seed + i * 919) * 1.9f;

                context.SpriteBatch.Draw(
                    pixel,
                    position,
                    source,
                    color * (flicker * opacity * context.Opacity),
                    angle,
                    new Vector2(0.5f, 0f),
                    new Vector2(width * context.Scale.X, crackLength),
                    SpriteEffects.None,
                    0f);
            }
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                uint x = (uint)seed;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00FFFFFF) / 16777215f;
            }
        }
    }

    public sealed class TextEchoLayer : IDynamicTextLayer
    {
        private readonly int echoCount;
        private readonly Vector2 travel;
        private readonly float period;
        private readonly float opacity;

        public TextEchoLayer(int echoCount, Vector2 travel, float period, float opacity)
        {
            this.echoCount = echoCount;
            this.travel = travel;
            this.period = period;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (echoCount <= 0 || period <= 0f)
                return;

            for (int i = 0; i < echoCount; i++)
            {
                float phase = i / (float)echoCount;
                float t = PositiveModulo(context.Time / period + phase, 1f);
                Color color = Color.Lerp(context.PrimaryColor, context.SecondaryColor, t) * ((1f - t) * opacity * context.Opacity);
                Vector2 offset = travel * t;

                ChatManager.DrawColorCodedString(
                    context.SpriteBatch,
                    context.Font,
                    context.Text,
                    context.Position + offset,
                    color,
                    context.Rotation,
                    context.Origin,
                    context.Scale);
            }
        }

        private static float PositiveModulo(float value, float modulus)
        {
            float result = value % modulus;
            return result < 0f ? result + modulus : result;
        }
    }

    public sealed class TextSparkleLayer : IDynamicTextLayer
    {
        private readonly int sparkleCount;
        private readonly float maxSize;
        private readonly float opacity;

        public TextSparkleLayer(int sparkleCount, float maxSize, float opacity)
        {
            this.sparkleCount = sparkleCount;
            this.maxSize = maxSize;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (sparkleCount <= 0 || context.Opacity <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 textSize = context.Font.MeasureString(context.Text) * context.Scale;

            for (int i = 0; i < sparkleCount; i++)
            {
                float x = Hash01(context.Seed + i * 101) * textSize.X;
                float y = Hash01(context.Seed + i * 211) * textSize.Y * 0.75f;
                float phase = Hash01(context.Seed + i * 307) * MathHelper.TwoPi;
                float brightness = MathF.Pow(0.5f + 0.5f * MathF.Sin(context.Time * 4.2f + phase), 3f);

                if (brightness <= 0.025f)
                    continue;

                Vector2 position = context.Position + new Vector2(x, y);
                Color color = Color.Lerp(context.PrimaryColor, Color.White, 0.65f) * (brightness * opacity * context.Opacity);
                float size = maxSize * (0.35f + brightness);

                DrawSpark(context.SpriteBatch, pixel, position, color, size);
            }
        }

        private static void DrawSpark(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, Color color, float size)
        {
            Rectangle source = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f);
            spriteBatch.Draw(pixel, position, source, color, 0f, origin, new Vector2(size, 1f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, position, source, color, MathHelper.PiOver2, origin, new Vector2(size, 1f), SpriteEffects.None, 0f);
        }

        private static float Hash01(int seed)
        {
            unchecked
            {
                uint x = (uint)seed;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00FFFFFF) / 16777215f;
            }
        }
    }

    public sealed class TextBloodDripLayer : IDynamicTextLayer
    {
        private readonly Color color;
        private readonly float cycle;
        private readonly float spread;
        private readonly float duration;
        private readonly float fallDistance;
        private readonly float opacity;

        public TextBloodDripLayer(Color color, float cycle, float spread, float duration, float fallDistance, float opacity)
        {
            this.color = color;
            this.cycle = cycle;
            this.spread = spread;
            this.duration = duration;
            this.fallDistance = fallDistance;
            this.opacity = opacity;
        }

        public void Draw(in DynamicTextDrawContext context)
        {
            if (string.IsNullOrEmpty(context.Text) || cycle <= 0f || duration <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float localCycle = PositiveModulo(context.Time * 1.45f + context.Seed * 0.09f, cycle);
            float charHeight = context.Font.MeasureString("A").Y * context.Scale.Y;
            float charOffset = 0f;

            for (int i = 0; i < context.Text.Length; i++)
            {
                string glyph = context.Text[i].ToString();
                float glyphWidth = context.Font.MeasureString(glyph).X * context.Scale.X;
                float start = i / (float)Math.Max(1, context.Text.Length) * spread;
                float localTime = PositiveModulo(localCycle - start, cycle);

                if (localTime >= 0f && localTime < duration)
                {
                    float progress = localTime / duration;
                    float fade = progress < 0.16f ? progress / 0.16f : progress > 0.72f ? 1f - (progress - 0.72f) / 0.28f : 1f;
                    fade = MathHelper.Clamp(fade, 0f, 1f);
                    float wobble = MathF.Sin(context.Time * 5.2f + i * 1.73f + context.Seed * 0.013f) * 1.2f * context.Scale.X;
                    float fall = SmoothStep(progress) * fallDistance;

                    Vector2 basePosition = context.Position + new Vector2(charOffset + glyphWidth * 0.5f + wobble, charHeight * 0.78f + fall);
                    DrawDrop(context.SpriteBatch, pixel, basePosition, color * (fade * opacity * context.Opacity), context.Scale.X, progress);
                }

                charOffset += glyphWidth;
            }
        }

        private static void DrawDrop(SpriteBatch spriteBatch, Texture2D pixel, Vector2 top, Color color, float scale, float progress)
        {
            Rectangle source = new(0, 0, 1, 1);
            Vector2 origin = new(0.5f);
            float stretch = 1f + progress * 0.7f;
            spriteBatch.Draw(pixel, top - Vector2.UnitY * 3.5f * scale, source, color * 0.38f, 0f, origin, new Vector2(0.8f, 5.4f * stretch) * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, top, source, color * 0.78f, 0f, origin, new Vector2(1.1f, 4.8f * stretch) * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, top + Vector2.UnitY * 4.4f * scale, source, color, 0f, origin, new Vector2(3.1f, 5.2f) * scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, top + Vector2.UnitY * 8f * scale, source, color * 0.78f, 0f, origin, new Vector2(1.8f, 2.6f) * scale, SpriteEffects.None, 0f);
        }

        private static float SmoothStep(float value)
        {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float PositiveModulo(float value, float modulus)
        {
            float result = value % modulus;
            return result < 0f ? result + modulus : result;
        }
    }
}
