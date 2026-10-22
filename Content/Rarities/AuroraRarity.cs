using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TestMod.Content.Rarities
{
    public sealed class AuroraRarity : ModRarity
    {
        public override Color RarityColor => new(101, 231, 220);
        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }
}
