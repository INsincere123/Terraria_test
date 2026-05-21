using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace TestMod.Common.DynamicText.Fonts
{
    public sealed class SystemDynamicTextFont : IDisposable
    {
        private const int TexturePadding = 4;
        private const int MaxCachedTextTextures = 256;

        private readonly object font;
        private readonly object privateFontCollection;
        private readonly Dictionary<string, CachedTextTexture> textureCache = [];
        private readonly Dictionary<string, Vector2> measureCache = [];
        private bool disposed;

        public string SourceDescription { get; }

        public SystemDynamicTextFont(object fontFamily, float size, object privateFontCollection, string sourceDescription)
        {
            font = SystemDrawingTextInterop.CreateFont(fontFamily, size);
            this.privateFontCollection = privateFontCollection;
            SourceDescription = sourceDescription;
        }

        public Vector2 MeasureString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Vector2.Zero;

            if (measureCache.TryGetValue(text, out Vector2 cached))
                return cached;

            Vector2 measured = MeasureStringUncached(text);
            measureCache[text] = measured;
            return measured;
        }

        public void Draw(SpriteBatch spriteBatch, string text, Vector2 position, XnaColor color, float rotation, Vector2 origin, Vector2 scale)
        {
            if (string.IsNullOrEmpty(text) || color.A <= 0)
                return;

            CachedTextTexture cached = GetTexture(text);
            if (cached.Texture is null || cached.Texture.IsDisposed)
                return;

            spriteBatch.Draw(
                cached.Texture,
                position,
                null,
                color,
                rotation,
                origin + new Vector2(TexturePadding),
                scale,
                SpriteEffects.None,
                0f);
        }

        public void DrawWithShadow(SpriteBatch spriteBatch, string text, Vector2 position, XnaColor color, float rotation, Vector2 origin, Vector2 scale, float spread)
        {
            DrawShadow(spriteBatch, text, position, XnaColor.Black * (color.A / 255f), rotation, origin, scale, spread);
            Draw(spriteBatch, text, position, color, rotation, origin, scale);
        }

        public void DrawShadow(SpriteBatch spriteBatch, string text, Vector2 position, XnaColor color, float rotation, Vector2 origin, Vector2 scale, float spread)
        {
            if (spread <= 0f)
                return;

            Draw(spriteBatch, text, position + new Vector2(-spread, 0f), color, rotation, origin, scale);
            Draw(spriteBatch, text, position + new Vector2(spread, 0f), color, rotation, origin, scale);
            Draw(spriteBatch, text, position + new Vector2(0f, -spread), color, rotation, origin, scale);
            Draw(spriteBatch, text, position + new Vector2(0f, spread), color, rotation, origin, scale);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            DisposeObject(font);
            DisposeObject(privateFontCollection);

            foreach (CachedTextTexture cached in textureCache.Values)
                cached.Texture?.Dispose();

            textureCache.Clear();
            measureCache.Clear();
        }

        private Vector2 MeasureStringUncached(string text)
        {
            object bitmap = null;
            object graphics = null;
            object format = null;

            try
            {
                bitmap = SystemDrawingTextInterop.CreateBitmap(1, 1);
                graphics = SystemDrawingTextInterop.CreateGraphics(bitmap);
                SystemDrawingTextInterop.SetTextRenderingHint(graphics);
                format = SystemDrawingTextInterop.CreateStringFormat();
                object size = SystemDrawingTextInterop.MeasureString(graphics, text, font, format);
                return new Vector2(
                    MathF.Max(1f, SystemDrawingTextInterop.GetSingleProperty(size, "Width")),
                    MathF.Max(1f, SystemDrawingTextInterop.GetSingleProperty(size, "Height")));
            }
            finally
            {
                DisposeObject(format);
                DisposeObject(graphics);
                DisposeObject(bitmap);
            }
        }

        private CachedTextTexture GetTexture(string text)
        {
            if (textureCache.TryGetValue(text, out CachedTextTexture cached) && cached.Texture?.IsDisposed == false)
                return cached;

            Vector2 measured = MeasureString(text);
            int width = Math.Max(1, (int)MathF.Ceiling(measured.X) + TexturePadding * 2);
            int height = Math.Max(1, (int)MathF.Ceiling(measured.Y) + TexturePadding * 2);

            object bitmap = null;
            object graphics = null;
            object brush = null;
            object format = null;

            try
            {
                bitmap = SystemDrawingTextInterop.CreateBitmap(width, height);
                graphics = SystemDrawingTextInterop.CreateGraphics(bitmap);
                brush = SystemDrawingTextInterop.CreateWhiteBrush();
                format = SystemDrawingTextInterop.CreateStringFormat();

                SystemDrawingTextInterop.ClearTransparent(graphics);
                SystemDrawingTextInterop.SetTextRenderingHint(graphics);
                SystemDrawingTextInterop.DrawString(graphics, text, font, brush, TexturePadding, TexturePadding, format);

                using MemoryStream stream = new();
                SystemDrawingTextInterop.SavePng(bitmap, stream);
                stream.Position = 0;

                Texture2D texture = Texture2D.FromStream(Main.graphics.GraphicsDevice, stream);
                cached = new CachedTextTexture(texture);
                TrimTextureCache();
                textureCache[text] = cached;
                return cached;
            }
            finally
            {
                DisposeObject(format);
                DisposeObject(brush);
                DisposeObject(graphics);
                DisposeObject(bitmap);
            }
        }

        private void TrimTextureCache()
        {
            while (textureCache.Count >= MaxCachedTextTextures)
            {
                string oldestKey = null;
                foreach (string key in textureCache.Keys)
                {
                    oldestKey = key;
                    break;
                }

                if (oldestKey is null)
                    return;

                textureCache[oldestKey].Texture?.Dispose();
                textureCache.Remove(oldestKey);
                measureCache.Remove(oldestKey);
            }
        }

        private static void DisposeObject(object value)
        {
            if (value is IDisposable disposable)
                disposable.Dispose();
        }

        private readonly struct CachedTextTexture
        {
            public readonly Texture2D Texture;

            public CachedTextTexture(Texture2D texture)
            {
                Texture = texture;
            }
        }
    }

    internal static class SystemDrawingTextInterop
    {
        private static readonly Type BitmapType = GetType("System.Drawing.Bitmap");
        private static readonly Type BrushType = GetType("System.Drawing.Brush");
        private static readonly Type ColorType = GetType("System.Drawing.Color");
        private static readonly Type FontType = GetType("System.Drawing.Font");
        private static readonly Type FontFamilyType = GetType("System.Drawing.FontFamily");
        private static readonly Type FontStyleType = GetType("System.Drawing.FontStyle");
        private static readonly Type GraphicsType = GetType("System.Drawing.Graphics");
        private static readonly Type GraphicsUnitType = GetType("System.Drawing.GraphicsUnit");
        private static readonly Type ImageFormatType = GetType("System.Drawing.Imaging.ImageFormat");
        private static readonly Type InstalledFontCollectionType = GetType("System.Drawing.Text.InstalledFontCollection");
        private static readonly Type PixelFormatType = GetType("System.Drawing.Imaging.PixelFormat");
        private static readonly Type PointFType = GetType("System.Drawing.PointF");
        private static readonly Type PrivateFontCollectionType = GetType("System.Drawing.Text.PrivateFontCollection");
        private static readonly Type SolidBrushType = GetType("System.Drawing.SolidBrush");
        private static readonly Type StringFormatType = GetType("System.Drawing.StringFormat");
        private static readonly Type StringFormatFlagsType = GetType("System.Drawing.StringFormatFlags");
        private static readonly Type TextRenderingHintType = GetType("System.Drawing.Text.TextRenderingHint");

        private static readonly MethodInfo FromImageMethod = GraphicsType.GetMethod("FromImage", BindingFlags.Public | BindingFlags.Static, null, [GetType("System.Drawing.Image")], null);
        private static readonly MethodInfo MeasureStringMethod = GraphicsType.GetMethod("MeasureString", BindingFlags.Public | BindingFlags.Instance, null, [typeof(string), FontType, PointFType, StringFormatType], null);
        private static readonly MethodInfo DrawStringMethod = GraphicsType.GetMethod("DrawString", BindingFlags.Public | BindingFlags.Instance, null, [typeof(string), FontType, BrushType, PointFType, StringFormatType], null);
        private static readonly MethodInfo SaveMethod = GetType("System.Drawing.Image").GetMethod("Save", BindingFlags.Public | BindingFlags.Instance, null, [typeof(Stream), ImageFormatType], null);

        public static object CreateFont(object family, float size)
        {
            object regular = Enum.Parse(FontStyleType, "Regular");
            object pixel = Enum.Parse(GraphicsUnitType, "Pixel");
            return Activator.CreateInstance(FontType, family, size, regular, pixel);
        }

        public static object CreateInstalledFontCollection()
        {
            return Activator.CreateInstance(InstalledFontCollectionType);
        }

        public static object CreatePrivateFontCollection()
        {
            return Activator.CreateInstance(PrivateFontCollectionType);
        }

        public static void AddFontFile(object collection, string path)
        {
            PrivateFontCollectionType.GetMethod("AddFontFile", [typeof(string)]).Invoke(collection, [path]);
        }

        public static Array GetFamilies(object fontCollection)
        {
            return (Array)fontCollection.GetType().GetProperty("Families").GetValue(fontCollection);
        }

        public static string GetFamilyName(object fontFamily)
        {
            return (string)FontFamilyType.GetProperty("Name").GetValue(fontFamily);
        }

        public static object CreateBitmap(int width, int height)
        {
            object format = Enum.Parse(PixelFormatType, "Format32bppArgb");
            return Activator.CreateInstance(BitmapType, width, height, format);
        }

        public static object CreateGraphics(object bitmap)
        {
            return FromImageMethod.Invoke(null, [bitmap]);
        }

        public static void SetTextRenderingHint(object graphics)
        {
            object hint = Enum.Parse(TextRenderingHintType, "AntiAliasGridFit");
            GraphicsType.GetProperty("TextRenderingHint").SetValue(graphics, hint);
        }

        public static void ClearTransparent(object graphics)
        {
            object transparent = ColorType.GetProperty("Transparent", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            GraphicsType.GetMethod("Clear", [ColorType]).Invoke(graphics, [transparent]);
        }

        public static object CreateWhiteBrush()
        {
            object white = ColorType.GetProperty("White", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            return Activator.CreateInstance(SolidBrushType, white);
        }

        public static object CreateStringFormat()
        {
            object genericTypographic = StringFormatType.GetProperty("GenericTypographic", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            object format = ((ICloneable)genericTypographic).Clone();
            PropertyInfo flagsProperty = StringFormatType.GetProperty("FormatFlags");
            object currentFlags = flagsProperty.GetValue(format);
            object trailingSpaces = Enum.Parse(StringFormatFlagsType, "MeasureTrailingSpaces");
            object combinedFlags = Enum.ToObject(StringFormatFlagsType, Convert.ToInt32(currentFlags) | Convert.ToInt32(trailingSpaces));
            flagsProperty.SetValue(format, combinedFlags);
            return format;
        }

        public static object MeasureString(object graphics, string text, object font, object format)
        {
            object point = Activator.CreateInstance(PointFType, 0f, 0f);
            return MeasureStringMethod.Invoke(graphics, [text, font, point, format]);
        }

        public static void DrawString(object graphics, string text, object font, object brush, float x, float y, object format)
        {
            object point = Activator.CreateInstance(PointFType, x, y);
            DrawStringMethod.Invoke(graphics, [text, font, brush, point, format]);
        }

        public static void SavePng(object bitmap, Stream stream)
        {
            object png = ImageFormatType.GetProperty("Png", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            SaveMethod.Invoke(bitmap, [stream, png]);
        }

        public static float GetSingleProperty(object value, string propertyName)
        {
            return Convert.ToSingle(value.GetType().GetProperty(propertyName).GetValue(value));
        }

        private static Type GetType(string name)
        {
            Type type = Type.GetType($"{name}, System.Drawing.Common", false);
            if (type is not null)
                return type;

            Assembly assembly = TryLoadSystemDrawingCommon();
            type = assembly?.GetType(name, false);
            if (type is not null)
                return type;

            throw new InvalidOperationException($"System.Drawing.Common type '{name}' is unavailable.");
        }

        private static Assembly TryLoadSystemDrawingCommon()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string sharedRoot = Path.Combine(programFiles, "dotnet", "shared", "Microsoft.WindowsDesktop.App");
            if (!Directory.Exists(sharedRoot))
                return null;

            foreach (string directory in Directory.GetDirectories(sharedRoot).OrderByDescending(Path.GetFileName))
            {
                string version = Path.GetFileName(directory);
                if (!version.StartsWith("8.", StringComparison.Ordinal))
                    continue;

                string dllPath = Path.Combine(directory, "System.Drawing.Common.dll");
                if (File.Exists(dllPath))
                    return Assembly.LoadFrom(dllPath);
            }

            foreach (string directory in Directory.GetDirectories(sharedRoot).OrderByDescending(Path.GetFileName))
            {
                string dllPath = Path.Combine(directory, "System.Drawing.Common.dll");
                if (File.Exists(dllPath))
                    return Assembly.LoadFrom(dllPath);
            }

            return null;
        }
    }
}
