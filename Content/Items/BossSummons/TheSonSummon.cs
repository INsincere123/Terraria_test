using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.BossSummons
{
    public class TheSonSummon : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) => ModLoader.HasMod("ContinentOfJourney");

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 19;
        }

        public override void SetDefaults()
        {
            Item.width = 26;
            Item.height = 30;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Purple;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
        }

        public override void ModifyResearchSorting(ref ContentSamples.CreativeHelper.ItemGroup itemGroup)
        {
            itemGroup = ContentSamples.CreativeHelper.ItemGroup.BossSpawners;
        }

        public override bool CanUseItem(Player player)
            => TheSonSummonSystem.CanSummon(player);

        public override bool ConsumeItem(Player player) => false;

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return null;

            if (!TheSonSummonSystem.CanSummon(player))
                return false;

            TheSonSummonSystem.RequestSummon(player);
            return true;
        }
    }
}
