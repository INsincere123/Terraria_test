using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using TestMod.Common.Graphics.DynamicText;

namespace TestMod.Content.Rarities
{
    public class AntaresRarity : ModRarity
    {
        public static readonly Color ColorA = new(30, 60, 180);
        public static readonly Color ColorB = new(90, 20, 160);

        public override Color RarityColor => Color.Lerp(ColorA, ColorB, 0.5f) * 2f;

        public static void Draw(Item item, DrawableTooltipLine line)
        {
            Draw(
                item,
                Main.spriteBatch,
                line.Text,
                line.X,
                line.Y,
                line.Rotation,
                line.Origin,
                line.BaseScale,
                Main.GlobalTimeWrappedHourly,
                FontAssets.MouseText.Value);
        }

        public static void Draw(
            Item item,
            SpriteBatch spriteBatch,
            string text,
            int x,
            int y,
            float rotation,
            Vector2 origin,
            Vector2 baseScale,
            float time,
            DynamicSpriteFont font)
        {
            DynamicTextTooltipRenderer.DrawText(
                spriteBatch,
                font,
                text,
                new Vector2(x, y),
                rotation,
                origin,
                baseScale,
                DynamicTextStyleRegistry.Get(DynamicTextStyleRegistry.RarityAntares),
                item.type * 733 + text.GetHashCode());
        }
    }
}
