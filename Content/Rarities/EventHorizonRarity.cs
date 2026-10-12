using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TestMod.Content.Rarities
{
    public class EventHorizonRarity : ModRarity
    {
        public static readonly Color TextCoreColor = new(20, 9, 38);
        public static readonly Color TextInnerColor = new(224, 237, 255);
        public static readonly Color AccretionGold = new(255, 194, 104);
        public static readonly Color AccretionOrange = new(255, 123, 48);

        public override Color RarityColor => TextInnerColor;
    }
}
