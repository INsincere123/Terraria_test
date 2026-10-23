using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public sealed class SpiritGuidancePlayer : ModPlayer
    {
        public bool Enabled;
        public override void ResetEffects() => Enabled = false;
        public override void UpdateDead() => Enabled = false;
        public override void OnEnterWorld() => Enabled = false;
    }
}
