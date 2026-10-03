using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace TestMod.Common.Graphics.DynamicText.Fonts
{
    public sealed partial class SystemDynamicTextFont
    {
        private const string NumericCharacters = "0123456789+-.,% ";
        private const int NumericPadding = 8;
        private const int AtlasColumns = 4;
        private Vector2[] numericSizes;
        private float[,] numericKerning;
        private Rectangle[] numericSources;
        private Texture2D numericTexture;

        internal static bool IsNumericText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            foreach (char character in text)
                if (NumericCharacters.IndexOf(character) < 0)
                    return false;
            return true;
        }

        private void EnsureNumericMetrics()
        {
            if (numericSizes is not null)
                return;
            object bitmap = null;
            object graphics = null;
            object format = null;
            try
            {
                bitmap = SystemDrawingTextInterop.CreateBitmap(1, 1);
                graphics = SystemDrawingTextInterop.CreateGraphics(bitmap);
                SystemDrawingTextInterop.SetTextRenderingHint(graphics);
                format = SystemDrawingTextInterop.CreateStringFormat();
                Vector2 Measure(string text)
                {
                    object size = SystemDrawingTextInterop.MeasureString(graphics, text, font, format);
                    return new Vector2(SystemDrawingTextInterop.GetSingleProperty(size, "Width"), SystemDrawingTextInterop.GetSingleProperty(size, "Height"));
                }
                int count = NumericCharacters.Length;
                Vector2[] sizes = new Vector2[count];
                float[,] kerning = new float[count, count];
                for (int i = 0; i < count; i++)
                    sizes[i] = Measure(NumericCharacters[i].ToString());
                // 保留字体的数字对字距，不能简单将单字宽度相加。
                for (int i = 0; i < count; i++)
                    for (int j = 0; j < count; j++)
                        kerning[i, j] = Measure(string.Concat(NumericCharacters[i], NumericCharacters[j])).X - sizes[i].X - sizes[j].X;
                numericKerning = kerning;
                numericSizes = sizes;
            }
            finally
            {
                DisposeObject(format);
                DisposeObject(graphics);
                DisposeObject(bitmap);
            }
        }

        private Vector2 MeasureNumericText(string text)
        {
            EnsureNumericMetrics();
            float width = 0f;
            float height = 0f;
            int previous = -1;
            foreach (char character in text)
            {
                int index = NumericCharacters.IndexOf(character);
                if (previous >= 0)
                    width += numericKerning[previous, index];
                width += numericSizes[index].X;
                height = MathF.Max(height, numericSizes[index].Y);
                previous = index;
            }
            return new Vector2(MathF.Max(1f, width), MathF.Max(1f, height));
        }

        internal float GetNumericKerning(char previous, char current)
        {
            int left = NumericCharacters.IndexOf(previous);
            int right = NumericCharacters.IndexOf(current);
            if (left < 0 || right < 0 || disposed)
                return 0f;
            EnsureNumericMetrics();
            return numericKerning[left, right];
        }

        private void EnsureNumericTexture()
        {
            if (numericTexture is not null && !numericTexture.IsDisposed)
                return;
            EnsureNumericMetrics();
            int cellWidth = 1;
            int cellHeight = 1;
            foreach (Vector2 size in numericSizes)
            {
                cellWidth = Math.Max(cellWidth, (int)MathF.Ceiling(size.X));
                cellHeight = Math.Max(cellHeight, (int)MathF.Ceiling(size.Y));
            }
            cellWidth += NumericPadding * 2;
            cellHeight += NumericPadding * 2;
            int width = cellWidth * AtlasColumns;
            int height = cellHeight * ((NumericCharacters.Length + AtlasColumns - 1) / AtlasColumns);
            object bitmap = null;
            object graphics = null;
            object brush = null;
            object format = null;
            Texture2D texture = null;
            try
            {
                bitmap = SystemDrawingTextInterop.CreateBitmap(width, height);
                graphics = SystemDrawingTextInterop.CreateGraphics(bitmap);
                brush = SystemDrawingTextInterop.CreateWhiteBrush();
                format = SystemDrawingTextInterop.CreateStringFormat();
                SystemDrawingTextInterop.ClearTransparent(graphics);
                SystemDrawingTextInterop.SetTextRenderingHint(graphics);
                Rectangle[] sources = new Rectangle[NumericCharacters.Length];
                for (int i = 0; i < NumericCharacters.Length; i++)
                {
                    int x = i % AtlasColumns * cellWidth;
                    int y = i / AtlasColumns * cellHeight;
                    sources[i] = new Rectangle(x, y, cellWidth, cellHeight);
                    SystemDrawingTextInterop.DrawString(graphics, NumericCharacters[i].ToString(), font, brush, x + NumericPadding, y + NumericPadding, format);
                }
                XnaColor[] pixels = SystemDrawingTextInterop.ReadPremultipliedPixels(bitmap, width, height);
                texture = new Texture2D(Main.graphics.GraphicsDevice, width, height);
                texture.SetData(pixels);
                numericSources = sources;
                numericTexture = texture;
                texture = null;
            }
            finally
            {
                texture?.Dispose();
                DisposeObject(format);
                DisposeObject(brush);
                DisposeObject(graphics);
                DisposeObject(bitmap);
            }
        }

        private bool TryDrawNumeric(SpriteBatch spriteBatch, string text, Vector2 position, XnaColor color, float rotation, Vector2 origin, Vector2 scale)
        {
            if (!IsNumericText(text))
                return false;
            EnsureNumericTexture();
            DrawNumericTexture(spriteBatch, numericTexture, text, position, color, rotation, origin, scale);
            return true;
        }

        private void DrawNumericTexture(SpriteBatch spriteBatch, Texture2D texture, string text, Vector2 position, XnaColor color, float rotation, Vector2 origin, Vector2 scale)
        {
            float offset = 0f;
            int previous = -1;
            foreach (char character in text)
            {
                int index = NumericCharacters.IndexOf(character);
                if (previous >= 0)
                    offset += numericKerning[previous, index];
                if (character != ' ')
                {
                    Vector2 shift = new Vector2(offset * scale.X, 0f).RotatedBy(rotation);
                    spriteBatch.Draw(texture, position + shift, numericSources[index], color, rotation,
                        origin + new Vector2(NumericPadding), scale, SpriteEffects.None, 0f);
                }
                offset += numericSizes[index].X;
                previous = index;
            }
        }

        internal bool TryDrawNumericOutline(SpriteBatch spriteBatch, string text, Vector2 position, XnaColor color,
            float rotation, Vector2 origin, Vector2 scale, float radius)
        {
            if (disposed || !IsNumericText(text) || radius <= 0f || scale.X <= 0f || scale.Y <= 0f)
                return false;
            if (color == XnaColor.Transparent)
                return true;
            EnsureNumericTexture();
            if (!DynamicTextOutlineSystem.TryGetMask(numericTexture, radius, scale, out Texture2D mask))
                return false;
            DrawNumericTexture(spriteBatch, mask, text, position, color, rotation, origin, scale);
            return true;
        }
    }
}
