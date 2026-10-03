using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TestMod.Content.Rarities
{
    public class AntaresRarity : ModRarity
    {
        public static readonly Color ColorA = new(30, 60, 180);
        public static readonly Color ColorB = new(90, 20, 160);

        public override Color RarityColor => Color.Lerp(ColorA, ColorB, 0.5f) * 2f;
    }
}
