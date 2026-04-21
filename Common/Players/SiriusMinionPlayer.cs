using Terraria.ModLoader;

namespace 武器test.Common.Players
{
    public class SiriusMinionPlayer : ModPlayer
    {
        public bool sirius;

        public override void ResetEffects()
        {
            sirius = false;
        }
    }
}