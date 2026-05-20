using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public class BlackHoleMinionPlayer : ModPlayer
    {
        public bool blackHoleMinion;
        public Vector2 BlackHoleOrbitCenter;
        public Vector2 BlackHoleOrbitTarget;
        public int BlackHoleOrbitTargetTimer;
        public ulong BlackHoleOrbitLastUpdateTick;

        public override void ResetEffects()
        {
            blackHoleMinion = false;
        }
    }
}
