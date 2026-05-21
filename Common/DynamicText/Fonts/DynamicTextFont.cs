using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria.UI.Chat;

namespace TestMod.Common.DynamicText.Fonts
{
    public readonly struct DynamicTextFont
    {
        private readonly DynamicSpriteFont fallbackFont;
        private readonly SystemDynamicTextFont systemFont;

        public bool UsesSystemFont => systemFont is not null;

        public DynamicTextFont(DynamicSpriteFont fallbackFont, SystemDynamicTextFont systemFont = null)
        {
            this.fallbackFont = fallbackFont;
            this.systemFont = systemFont;
        }

        public Vector2 MeasureString(string text)
        {
            if (systemFont is not null)
                return systemFont.MeasureString(text);

            return fallbackFont.MeasureString(text);
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

            ChatManager.DrawColorCodedStringShadow(spriteBatch, fallbackFont, text, position, color, rotation, origin, scale, maxWidth, spread);
        }
    }
}
