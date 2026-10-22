using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Items.BossSummons
{
    public abstract class HomewardJourneyBossSummon : ModItem
    {
        protected abstract string BossInternalName { get; }
        protected abstract string BossTexturePath { get; }

        // 使用常规头部图标，不随 Boss 的战斗阶段改变物品外观。
        public override string Texture => BossTexturePath + "_Head_Boss";

        public override bool IsLoadingEnabled(Mod mod) => ModLoader.HasMod("ContinentOfJourney");

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 19;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
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
            => player.active && !player.dead && TryGetBossType(out int bossType) && !NPC.AnyNPCs(bossType);

        public override bool ConsumeItem(Player player) => false;

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return null;

            if (!player.active || player.dead || !TryGetBossType(out int bossType) || NPC.AnyNPCs(bossType))
                return false;

            // 这些 Boss 均允许原版多人召唤消息，由服务器生成并检查同类重复。
            if (Main.netMode == NetmodeID.SinglePlayer)
                NPC.SpawnOnPlayer(player.whoAmI, bossType);
            else if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent,
                    number: player.whoAmI, number2: bossType);

            return true;
        }

        private bool TryGetBossType(out int bossType)
        {
            bossType = 0;
            if (!ModLoader.TryGetMod("ContinentOfJourney", out Mod journey)
                || !journey.TryFind<ModNPC>(BossInternalName, out var boss))
                return false;

            bossType = boss.Type;
            return true;
        }
    }
}
