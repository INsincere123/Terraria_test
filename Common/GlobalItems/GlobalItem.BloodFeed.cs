using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        public override void OnConsumeItem(Item item, Player player)
        {
            player.GetModPlayer<BloodFeedPlayer>().CleanseAfterHealingItemConsumed(item);
        }

        private static void ApplyBloodFeedHitModifiers(Player player, ref NPC.HitModifiers modifiers)
        {
            if (player.GetModPlayer<BloodFeedPlayer>().IsBerserk)
                modifiers.ScalingArmorPenetration += 1f;
        }

        public override float UseTimeMultiplier(Item item, Player player)
        {
            BloodFeedPlayer bloodFeed = player.GetModPlayer<BloodFeedPlayer>();
            float armorMultiplier = player.GetModPlayer<TerraArmorPlayer>().FullSet ? 0.8f : 1f;
            if (bloodFeed.IsBerserk)
                return armorMultiplier / 5f;
            if (bloodFeed.IsExhausted)
                return armorMultiplier * 2f;

            return armorMultiplier;
        }

        public override float UseAnimationMultiplier(Item item, Player player)
        {
            BloodFeedPlayer bloodFeed = player.GetModPlayer<BloodFeedPlayer>();
            float armorMultiplier = player.GetModPlayer<TerraArmorPlayer>().FullSet ? 0.8f : 1f;
            if (bloodFeed.IsBerserk)
                return armorMultiplier / 5f;
            if (bloodFeed.IsExhausted)
                return armorMultiplier * 2f;

            return armorMultiplier;
        }
    }
}
