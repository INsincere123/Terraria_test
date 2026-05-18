using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        private static void ApplyBloodFeedHitModifiers(Player player, ref NPC.HitModifiers modifiers)
        {
            if (player.GetModPlayer<BloodFeedPlayer>().IsBerserk)
                modifiers.ScalingArmorPenetration += 1f;
        }

        public override float UseTimeMultiplier(Item item, Player player)
        {
            BloodFeedPlayer bloodFeed = player.GetModPlayer<BloodFeedPlayer>();
            if (bloodFeed.IsBerserk)
                return 1f / 5f;
            if (bloodFeed.IsExhausted)
                return 2f;

            return 1f;
        }

        public override float UseAnimationMultiplier(Item item, Player player)
        {
            BloodFeedPlayer bloodFeed = player.GetModPlayer<BloodFeedPlayer>();
            if (bloodFeed.IsBerserk)
                return 1f / 5f;
            if (bloodFeed.IsExhausted)
                return 2f;

            return 1f;
        }
    }
}
