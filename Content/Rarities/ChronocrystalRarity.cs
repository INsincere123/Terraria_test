using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TestMod.Content.Rarities
{
    public sealed class ChronocrystalRarity : ModRarity
    {
        public override Color RarityColor => new(190, 225, 248);
        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }
}
