using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public class AntaresMinionPlayer : ModPlayer
    {
        public bool antares;

        public override void ResetEffects()
        {
            antares = false;
        }
    }
}