using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TestMod.Content.Rarities
{
    public sealed class TerraRarity : ModRarity
    {
        public static readonly Color Emerald = new(36, 184, 106);

        public override Color RarityColor => Emerald;
        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }
}
