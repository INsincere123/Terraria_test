using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TestMod.Content.Rarities
{
    public class EventHorizonRarity : ModRarity
    {
        public static readonly Color TextCoreColor = new(46, 18, 66);
        public static readonly Color TextInnerColor = new(118, 52, 158);
        public static readonly Color AccretionGold = new(255, 166, 55);
        public static readonly Color AccretionOrange = new(255, 95, 34);

        public override Color RarityColor => Color.Lerp(TextInnerColor, AccretionGold, 0.35f);
    }
}
