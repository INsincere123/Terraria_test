using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.BossSummons
{
    public sealed class WallofShadowSummon : ModItem
    {
        public override string Texture => "ContinentOfJourney/NPCs/Boss_WallofShadow/WallofShadow_Head_Boss";
        public override bool IsLoadingEnabled(Mod mod) => ModLoader.HasMod("ContinentOfJourney");
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 19;
        }
        public override void SetDefaults()
        {
            Item.width = Item.height = 32;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Purple;
            Item.useTime = Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
        }
        public override bool ConsumeItem(Player player) => false;
        // 失败原因在实际使用时反馈；不能在CanUseItem中提前拦下，否则UseItem不会执行。
        public override bool CanUseItem(Player player) => player.active && !player.dead;
        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer) return null;
            WallofShadowArenaSystem.Request(player);
            return true;
        }
        public override void ModifyResearchSorting(ref ContentSamples.CreativeHelper.ItemGroup group)
            => group = ContentSamples.CreativeHelper.ItemGroup.BossSpawners;
    }
}
