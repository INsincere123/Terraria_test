using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    public class DefenseDamageProtectionPlayer : ModPlayer
    {
        internal bool Immune;
        internal bool Halved;

        public override void ResetEffects() => Clear();
        public override void UpdateDead() => Clear();
        public override void OnEnterWorld() => Clear();

        private void Clear() => Immune = Halved = false;

        public override void PostUpdateEquips()
        {
            if (Immune && Player.active && !Player.dead)
                CalamityDefenseDamageSystem.ApplyImmunity(Player);
        }
    }
}
