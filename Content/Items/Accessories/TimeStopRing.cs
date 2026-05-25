using System.Collections.Generic;
using TestMod.Common.Systems;
using TestMod.Common.Mechanics.AccessoryEffects;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Accessories
{
    /// <summary>
    /// 时停戒指 —— 测试用饰品，装备后可按绑定键触发时停。
    /// 时停期间所有敌方 NPC 和敌方弹幕完全静止，玩家不受影响。
    /// 持续 3 秒，冷却 30 秒。
    /// </summary>
    public class TimeStopRing : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 22;
            Item.accessory = true;
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.value = Item.sellPrice(gold: 5);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            TimeStopEffect.Grant(player);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string keyText = KeybindUtils.GetKeyText(TimeStopKeybinds.TimeStopKey);
            tooltips.Add(new TooltipLine(Mod, "TimeStopDesc",
                $"按 [{keyText}] 触发时停\n" +
                $"时停期间所有敌方和敌方弹幕完全静止，玩家不受影响\n" +
                $"持续 3 秒，冷却 30 秒"));
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