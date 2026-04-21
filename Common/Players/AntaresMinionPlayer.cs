using Terraria.ModLoader;

namespace 武器test.Common.Players
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