using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.DynamicText
{
    public static class DynamicTextTooltipRenderer
    {
        public static bool TryDrawDamageLine(Item item, DrawableTooltipLine line)
        {
            if (!DynamicTextStyleRegistry.TryGetTooltipDamageStyle(item.DamageType, out DynamicTextStyle style))
                return false;

            DrawLine(line, style, item.type * 397 + line.Text.GetHashCode());
            return true;
        }

        public static void DrawLine(DrawableTooltipLine line, DynamicTextStyle style, int seed)
        {
            DrawText(
                Main.spriteBatch,
                line.Font,
                line.Text,
                new Vector2(line.X, line.Y),
                line.Rotation,
                line.Origin,
                line.BaseScale,
                style,
                seed,
                line.OverrideColor,
                line.MaxWidth,
                line.Spread,
                DynamicTextSurface.Tooltip);
        }

        public static void DrawText(
            SpriteBatch spriteBatch,
            DynamicSpriteFont font,
            string text,
            Vector2 position,
            float rotation,
            Vector2 origin,
            Vector2 scale,
            DynamicTextStyle style,
            int seed,
            Color? overrideColor = null,
            float maxWidth = -1f,
            float spread = 1f,
            DynamicTextSurface surface = DynamicTextSurface.Tooltip)
        {
            Color primary = overrideColor ?? style.PrimaryColor;
            DynamicTextDrawContext context = new(
                spriteBatch,
                font,
                text,
                position,
                rotation,
                origin,
                scale,
                Main.GlobalTimeWrappedHourly,
                0f,
                1f,
                seed,
                maxWidth,
                spread,
                primary,
                style.SecondaryColor,
                style.ShadowColor,
                surface);

            style.Draw(context);
        }
    }
}
