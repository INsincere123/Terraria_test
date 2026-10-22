using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TestMod.Content.Rarities
{
    public sealed class EmberCrownRarity : ModRarity
    {
        public override Color RarityColor => new(242, 103, 55);
        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }
}
