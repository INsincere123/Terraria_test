using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria.UI.Chat;

namespace TestMod.Common.Graphics.DynamicText.Fonts
{
    public readonly struct DynamicTextFont : IEquatable<DynamicTextFont>
    {
        private readonly DynamicSpriteFont fallbackFont;
        private readonly SystemDynamicTextFont systemFont;

        public bool UsesSystemFont => systemFont is not null;

        public bool Equals(DynamicTextFont other) => ReferenceEquals(fallbackFont, other.fallbackFont) && ReferenceEquals(systemFont, other.systemFont);
        public override bool Equals(object obj) => obj is DynamicTextFont other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(fallbackFont, systemFont);

        internal float GetGlyphKerning(string previous, string current) =>
            systemFont is not null && previous.Length == 1 && current.Length == 1 ? systemFont.GetNumericKerning(previous[0], current[0]) : 0f;

        internal bool TryDrawOutline(in DynamicTextDrawContext context, Color color, float radius) =>
            systemFont is not null && systemFont.TryDrawNumericOutline(context.SpriteBatch, context.Text, context.Position,
                color, context.Rotation, context.Origin, context.Scale, radius);

        public DynamicTextFont ForText(string text, float maxWidth = -1f) =>
            UsesSystemFont && DynamicTextLayout.RequiresChatLayout(text, maxWidth) ? new DynamicTextFont(fallbackFont) : this;

        internal Vector2 MeasureChatLayout(TextSnippet[] snippets, float maxWidth) =>
            ChatManager.GetStringSize(fallbackFont, snippets, Vector2.One, maxWidth);

        internal void DrawLayout(SpriteBatch spriteBatch, DynamicTextLayout layout, Vector2 position, Color color,
            float rotation, Vector2 origin, Vector2 scale, float maxWidth, bool ignoreColors = false)
        {
            if (systemFont is not null)
            {
                systemFont.Draw(spriteBatch, layout.Text, position, color, rotation, origin, scale);
                return;
            }
            if (layout.Snippets is null)
            {
                spriteBatch.DrawString(fallbackFont, layout.Text, position, color, rotation, origin, scale, SpriteEffects.None, 0f);
                return;
            }
            if (!ignoreColors)
            {
                for (int i = 0; i < layout.Snippets.Length; i++)
                    layout.Snippets[i].Color = layout.DefaultColors[i] ? color : layout.SnippetColors[i] * (color.A / 255f);
            }
            ChatManager.DrawColorCodedString(spriteBatch, fallbackFont, layout.Snippets, position, color,
                rotation, origin, scale, out _, maxWidth, ignoreColors);
        }

        internal void DrawGlyph(SpriteBatch spriteBatch, string glyph, Vector2 position, Color color,
            float rotation, Vector2 origin, Vector2 scale)
        {
            if (systemFont is not null)
                systemFont.Draw(spriteBatch, glyph, position, color, rotation, origin, scale);
            else
                spriteBatch.DrawString(fallbackFont, glyph, position, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }

        public DynamicTextFont(DynamicSpriteFont fallbackFont, SystemDynamicTextFont systemFont = null)
        {
            this.fallbackFont = fallbackFont;
            this.systemFont = systemFont;
        }

        public Vector2 MeasureString(string text)
        {
            if (systemFont is not null)
                return systemFont.MeasureString(text);

            return text.Contains('[')
                ? ChatManager.GetStringSize(fallbackFont, text, Vector2.One)
                : fallbackFont.MeasureString(text);
        }

        public void DrawColorCodedString(
            SpriteBatch spriteBatch,
            string text,
            Vector2 position,
            Color color,
            float rotation,
            Vector2 origin,
            Vector2 scale,
            float maxWidth = -1f,
            bool ignoreColors = false)
        {
            if (systemFont is not null)
            {
                systemFont.Draw(spriteBatch, text, position, color, rotation, origin, scale);
                return;
            }

            if (text.Contains('['))
                ChatManager.DrawColorCodedString(spriteBatch, fallbackFont, ChatManager.ParseMessage(text, color).ToArray(), position, color, rotation, origin, scale, out _, maxWidth, ignoreColors);
            else
                ChatManager.DrawColorCodedString(spriteBatch, fallbackFont, text, position, color, rotation, origin, scale, maxWidth, ignoreColors);
        }

        public void DrawColorCodedStringWithShadow(
            SpriteBatch spriteBatch,
            string text,
            Vector2 position,
            Color color,
            float rotation,
            Vector2 origin,
            Vector2 scale,
            float maxWidth = -1f,
            float spread = 2f)
        {
            if (systemFont is not null)
            {
                systemFont.DrawWithShadow(spriteBatch, text, position, color, rotation, origin, scale, spread);
                return;
            }

            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, fallbackFont, text, position, color, rotation, origin, scale, maxWidth, spread);
        }

        public void DrawColorCodedStringShadow(
            SpriteBatch spriteBatch,
            string text,
            Vector2 position,
            Color color,
            float rotation,
            Vector2 origin,
            Vector2 scale,
            float maxWidth = -1f,
            float spread = 2f)
        {
            if (systemFont is not null)
            {
                systemFont.DrawShadow(spriteBatch, text, position, color, rotation, origin, scale, spread);
                return;
            }

            if (text.Contains('['))
                ChatManager.DrawColorCodedStringShadow(spriteBatch, fallbackFont, ChatManager.ParseMessage(text, color).ToArray(), position, color, rotation, origin, scale, maxWidth, spread);
            else
                ChatManager.DrawColorCodedStringShadow(spriteBatch, fallbackFont, text, position, color, rotation, origin, scale, maxWidth, spread);
        }
    }
}
