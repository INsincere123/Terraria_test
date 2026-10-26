using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public sealed class SpiritGuidancePlayer : ModPlayer
    {
        public bool Enabled;
        public bool ModdedShotsEnabled;
        public override void ResetEffects() => Clear();
        public override void UpdateDead() => Clear();
        public override void OnEnterWorld() => Clear();
        private void Clear() => Enabled = ModdedShotsEnabled = false;
    }
}
