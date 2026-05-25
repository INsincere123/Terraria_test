using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Armor
{
    [AutoloadEquip(EquipType.Body)]
    public class AntaresBreastplate : ModItem
    {
        // ╔══════════════════════════════════════════════════════╗
        // ║              胸甲数值调整区域                        ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【基础防御】                                        ║
        // ║    Defense              物品本身的防御力             ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【攻击属性】  适用于所有职业                        ║
        // ║    DamageBonus          +X% 伤害，1.0f = +100%      ║
        // ║    CritBonus            +X% 暴击率                  ║
        // ║    AttackSpeedBonus     +X% 攻速，0.35f = +35%      ║
        // ║    ArmorPenetration     +X 穿甲值                   ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【防御属性】                                        ║
        // ║    MaxLifeBonus         +X% 最大生命值上限           ║
        // ║    MaxManaBonus         +X% 最大法力值上限           ║
        // ║    DamageReductionBonus +X% 免伤（乘法叠加）         ║
        // ║    DefenseBonus         +X 防御值（加法叠加）        ║
        // ║    LifeRegenBonus       +X 生命再生（2 = +1 HP/s）   ║
        // ║    ManaRegenBonus       +X 法力恢复倍率              ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【召唤属性】                                        ║
        // ║    ExtraMinionSlots     +X 召唤物上限槽位            ║
        // ║    ExtraSentrySlots     +X 哨兵上限槽位              ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【移动属性】                                        ║
        // ║    MoveSpeedBonus       +X% 移动速度                 ║
        // ╚══════════════════════════════════════════════════════╝

        public const int   Defense              = 100;

        public const float DamageBonus          = 0.33f;
        public const int   CritBonus            = 12;
        public const float AttackSpeedBonus     = 0.12f;
        public const int   ArmorPenetration     = 8;

        public const float MaxLifeBonus         = 0.33f;
        public const float MaxManaBonus         = 0.33f;
        public const float DamageReductionBonus = 0.05f;
        public const int   DefenseBonus         = 5;
        public const int   LifeRegenBonus       = 10;
        public const float ManaRegenBonus       = 1f;

        public const int   ExtraMinionSlots     = 3;
        public const int   ExtraSentrySlots     = 1;

        //public const float MoveSpeedBonus       = 0.03f;

        // ══════════════════════════════════════════════════════

        public override void SetDefaults()
        {
            Item.width   = 26;
            Item.height  = 22;
            Item.value   = 1000000;
            Item.rare    = ItemRarityID.Red;
            if (CalamityCompatSystem.CalamityLoaded)
                Item.rare = CalamityCompatSystem.CalamityRarity;
            Item.defense = Defense;
        }

        public override void UpdateEquip(Player player)
        {
            // 攻击属性
            player.GetDamage(DamageClass.Generic)           += DamageBonus;
            player.GetCritChance(DamageClass.Generic)       += CritBonus;
            player.GetAttackSpeed(DamageClass.Generic)      += AttackSpeedBonus;
            player.GetArmorPenetration(DamageClass.Generic) += ArmorPenetration;

            // 防御属性
            player.DefenseEffectiveness *= (1f + DamageReductionBonus);
            player.statDefense          += DefenseBonus;
            player.lifeRegen            += LifeRegenBonus;
            player.manaRegenBonus       += (int)ManaRegenBonus;

            // 召唤属性
            player.maxMinions += ExtraMinionSlots;
            player.maxTurrets += ExtraSentrySlots;

            // 移动属性
            //player.moveSpeed  += MoveSpeedBonus;

            // 标记供 AntaresArmorPlayer 处理百分比生命/法力加成
            player.GetModPlayer<AntaresArmorPlayer>().wearingBreastplate = true;
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
