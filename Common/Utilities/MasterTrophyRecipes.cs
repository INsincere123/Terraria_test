using Terraria;
using Terraria.ID;

namespace TestMod.Common.Utilities
{
    internal static class MasterTrophyRecipes
    {
        internal static void Register(int resultType)
        {
            Add(resultType, ItemID.EaterofWorldsMasterTrophy);
            Add(resultType, ItemID.BrainofCthulhuMasterTrophy);
        }

        private static void Add(int resultType, int eowOrBoc)
        {
            Recipe recipe = Recipe.Create(resultType);

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
