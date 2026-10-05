using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    public sealed class TheDarkImmunityPlayer : ModPlayer
    {
        public bool Enabled;

        public override void ResetEffects() => Enabled = false;
        public override void UpdateDead() => Enabled = false;
        public override void OnEnterWorld() => Enabled = false;

        public override void PostUpdateEquips()
        {
            // 旅人归途在 PostUpdateBuffs 中更新计时；装备结算后覆盖，避开模组加载顺序。
            if (Enabled && !Player.dead)
                HomewardJourneyCompatSystem.RemoveTheDarkVeil(Player);
        }
    }
}
