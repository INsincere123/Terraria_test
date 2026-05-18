using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Rarities;
using TestMod.Common.Players;

namespace TestMod.Items
{
    public class StatBoostToggle : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.value = Item.buyPrice(0, 10, 0, 0);
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.UseSound = SoundID.Item4;
            Item.autoReuse = false;
        }

        public override bool? UseItem(Player player)
        {
            player.GetModPlayer<GodModePlayer>().GodModeBuff =
                !player.GetModPlayer<GodModePlayer>().GodModeBuff;
            return true;
        }

        public override void AddRecipes()
        {
            // 世界吞噬怪 / 克苏鲁之脑 二选一（腐化 or 猩红世界）
            AddMasterTrophyRecipe(ItemID.EaterofWorldsMasterTrophy);
            AddMasterTrophyRecipe(ItemID.BrainofCthulhuMasterTrophy);
        }

        private void AddMasterTrophyRecipe(int eowOrBoc)
        {
            Recipe recipe = Recipe.Create(Type);

            recipe.AddIngredient(ItemID.EyeofCthulhuMasterTrophy);   // 克苏鲁之眼
            recipe.AddIngredient(eowOrBoc);                           // 世界吞噬怪 或 克苏鲁之脑
            recipe.AddIngredient(ItemID.SkeletronMasterTrophy);       // 骷髅王
            recipe.AddIngredient(ItemID.QueenBeeMasterTrophy);        // 蜂王
            recipe.AddIngredient(ItemID.KingSlimeMasterTrophy);       // 史莱姆王
            recipe.AddIngredient(ItemID.WallofFleshMasterTrophy);     // 血肉墙
            recipe.AddIngredient(ItemID.TwinsMasterTrophy);           // 双子魔眼
            recipe.AddIngredient(ItemID.DestroyerMasterTrophy);       // 毁灭者
            recipe.AddIngredient(ItemID.SkeletronPrimeMasterTrophy);  // 机械骷髅王
            recipe.AddIngredient(ItemID.PlanteraMasterTrophy);        // 世纪之花
            recipe.AddIngredient(ItemID.GolemMasterTrophy);           // 石巨人
            recipe.AddIngredient(ItemID.DukeFishronMasterTrophy);     // 猪龙鱼公爵
            recipe.AddIngredient(ItemID.LunaticCultistMasterTrophy);  // 拜月教邪教徒
            recipe.AddIngredient(ItemID.MoonLordMasterTrophy);        // 月亮领主
            recipe.AddIngredient(ItemID.UFOMasterTrophy);             // 火星飞碟
            recipe.AddIngredient(ItemID.FlyingDutchmanMasterTrophy);  // 荷兰飞盗船
            recipe.AddIngredient(ItemID.MourningWoodMasterTrophy);    // 哀木
            recipe.AddIngredient(ItemID.PumpkingMasterTrophy);        // 南瓜王
            recipe.AddIngredient(ItemID.IceQueenMasterTrophy);        // 冰雪女王
            recipe.AddIngredient(ItemID.EverscreamMasterTrophy);      // 常绿尖叫怪
            recipe.AddIngredient(ItemID.SantankMasterTrophy);         // 圣诞坦克
            recipe.AddIngredient(ItemID.DarkMageMasterTrophy);        // 暗黑魔法师
            recipe.AddIngredient(ItemID.OgreMasterTrophy);            // 食人魔
            recipe.AddIngredient(ItemID.BetsyMasterTrophy);           // 双足翼龙
            recipe.AddIngredient(ItemID.FairyQueenMasterTrophy);      // 光之女皇
            recipe.AddIngredient(ItemID.QueenSlimeMasterTrophy);      // 史莱姆皇后
            recipe.AddIngredient(ItemID.DeerclopsMasterTrophy);       // 独眼巨鹿

            recipe.AddTile(TileID.LunarCraftingStation);
            recipe.Register();
        }
    }
}
