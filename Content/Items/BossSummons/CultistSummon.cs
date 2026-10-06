using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.BossSummons
{
    public class CultistSummon : ModItem
    {
        private const string ModeSaveKey = "spawnPillars";
        private const string LocalizationPrefix = "Mods.TestMod.Items.CultistSummon.";

        // 值类型状态随 ModItem.Clone 复制；新物品及旧存档默认模式一。
        public bool SpawnPillars { get; private set; }

        public override string Texture => $"Terraria/Images/Item_{ItemID.BossMaskCultist}";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 18;
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
        }

        public override void ModifyResearchSorting(ref ContentSamples.CreativeHelper.ItemGroup itemGroup)
        {
            itemGroup = ContentSamples.CreativeHelper.ItemGroup.BossSpawners;
        }

        public override bool CanUseItem(Player player) => CultistSummonSystem.CanSummon(player);

        public override bool CanRightClick() => true;

        public override void RightClick(Player player)
        {
            SpawnPillars = !SpawnPillars;
            Item.NetStateChanged();
        }

        public override bool ConsumeItem(Player player) => false;

        public override void SaveData(TagCompound tag) => tag[ModeSaveKey] = SpawnPillars;

        public override void LoadData(TagCompound tag)
            => SpawnPillars = tag.ContainsKey(ModeSaveKey) && tag.GetBool(ModeSaveKey);

        public override void NetSend(BinaryWriter writer) => writer.Write(SpawnPillars);

        public override void NetReceive(BinaryReader reader) => SpawnPillars = reader.ReadBoolean();

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                CultistSummonSystem.RequestSummon(player, SpawnPillars);
            return true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "SummonMode",
                Language.GetTextValue(LocalizationPrefix + (SpawnPillars ? "ModeTwo" : "ModeOne"))));
            tooltips.Add(new TooltipLine(Mod, "ModeToggleHint",
                Language.GetTextValue(LocalizationPrefix + "ToggleHint")));
            if (!CultistSummonSystem.IsReady)
                tooltips.Add(new TooltipLine(Mod, "SummonUnavailable",
                    Language.GetTextValue("Mods.TestMod.Items.CultistSummon.Unavailable")));
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Silk, 10)
                .AddIngredient(ItemID.Book, 5)
                .AddTile(TileID.DemonAltar)
                .Register();
        }
    }
}
