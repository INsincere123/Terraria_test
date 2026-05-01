using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using 武器test.Common.Players;
using 武器test.Common.Systems;

namespace 武器test.Items.Armor
{
    [AutoloadEquip(EquipType.Head)]
    public class AntaresHelmet : ModItem
    {
        // ╔══════════════════════════════════════════════════════╗
        // ║              头盔数值调整区域                        ║
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
        // ╠══════════════════════════════════════════════════════╣
        // ║  【套装效果参数】                                    ║
        // ║    ReviveCooldown       复活 CD（帧，60帧 = 1秒）    ║
        // ║    ReviveLifePercent    复活后恢复的血量（占最大值）  ║
        // ║    无视秒杀伤害，逻辑见 AntaresArmorPlayer         ║
        // ╚══════════════════════════════════════════════════════╝

        public const int Defense = 60;

        public const float DamageBonus = 0.33f;
        public const int CritBonus = 12;
        public const float AttackSpeedBonus = 0.12f;
        public const int ArmorPenetration = 8;

        public const float MaxLifeBonus = 0.33f;
        public const float MaxManaBonus = 0.33f;
        public const float DamageReductionBonus = 0.05f;
        public const int DefenseBonus = 5;
        public const int LifeRegenBonus = 10;
        // public const float ManaRegenBonus    = 0.33f;  // 暂时注释

        public const int ExtraMinionSlots = 3;
        public const int ExtraSentrySlots = 1;

        // public const float MoveSpeedBonus    = 0.04f;  // 暂时注释

        // ---------- 套装效果参数 ----------
        public const int ReviveCooldown = 60 * 60 * 2;  // 2 分钟，单位帧
        public const float ReviveLifePercent = 0.50f;   // 复活后回满最大血量的 50%

        // ══════════════════════════════════════════════════════

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 20;
            Item.value = 1000000;
            Item.rare = ItemRarityID.Red;
            if (CalamityCompatSystem.CalamityLoaded)
                Item.rare = CalamityCompatSystem.CalamityRarity;
            Item.defense = Defense;
        }

        // 判断是否构成套装（头盔 + 胸甲 + 腿甲）
        public override bool IsArmorSet(Item head, Item body, Item legs)
            => body.type == ModContent.ItemType<AntaresBreastplate>()
            && legs.type == ModContent.ItemType<AntaresLeggings>();

        // 套装激活时每帧调用，标记 ModPlayer 启用套装效果
        public override void UpdateArmorSet(Player player)
        {
            string key = AntaresKeybinds.GravityWellKey.GetAssignedKeys().Count > 0
                ? AntaresKeybinds.GravityWellKey.GetAssignedKeys()[0]
                : "未绑定";

            string calamityBonus = CalamityCompatSystem.CalamityLoaded
                ? "\n灾厄兼容：移除跨职业召唤伤害惩罚"
                : "";

            player.setBonus = $"致命伤害后复活（冷却2分钟），恢复50%生命值，无敌3秒\n免疫秒杀，若单次伤害大于你的最大生命值，则此伤害为1\n按下 [{key}] 激活引力井，持续拉取周围敌人{calamityBonus}";
            player.GetModPlayer<AntaresArmorPlayer>().wearingFullSet = true;
            CalamityCompatSystem.DisableSummonPenalty(player);
        }

        public override void UpdateEquip(Player player)
        {
            // 攻击属性
            player.GetDamage(DamageClass.Generic) += DamageBonus;
            player.GetCritChance(DamageClass.Generic) += CritBonus;
            player.GetAttackSpeed(DamageClass.Generic) += AttackSpeedBonus;
            player.GetArmorPenetration(DamageClass.Generic) += ArmorPenetration;

            // 防御属性
            player.DefenseEffectiveness *= (1f + DamageReductionBonus);
            player.statDefense += DefenseBonus;
            player.lifeRegen += LifeRegenBonus;
            // player.manaRegenBonus     += ManaRegenBonus;  // 暂时注释

            // 召唤属性
            player.maxMinions += ExtraMinionSlots;
            player.maxTurrets += ExtraSentrySlots;

            // 移动属性
            // player.moveSpeed  += MoveSpeedBonus;  // 暂时注释

            // 标记供 AntaresArmorPlayer 处理百分比生命/法力加成
            player.GetModPlayer<AntaresArmorPlayer>().wearingHelmet = true;
        }

        public override void AddRecipes()
        {
            Recipe recipe = Recipe.Create(Type);

            recipe.AddIngredient(4924); // 克苏鲁之眼
            recipe.AddIngredient(4925); // 世界吞噬怪
            recipe.AddIngredient(4926); // 克苏鲁之脑
            recipe.AddIngredient(4927); // 骷髅王
            recipe.AddIngredient(4928); // 蜂王
            recipe.AddIngredient(4929); // 史莱姆王
            recipe.AddIngredient(4930); // 血肉墙
            recipe.AddIngredient(4931); // 双子魔眼
            recipe.AddIngredient(4932); // 毁灭者
            recipe.AddIngredient(4933); // 机械骷髅王
            recipe.AddIngredient(4934); // 世纪之花
            recipe.AddIngredient(4935); // 石巨人
            recipe.AddIngredient(4936); // 猪龙鱼公爵
            recipe.AddIngredient(4937); // 拜月教邪教徒
            recipe.AddIngredient(4938); // 月亮领主
            recipe.AddIngredient(4939); // 火星飞碟
            recipe.AddIngredient(4940); // 荷兰飞盗船
            recipe.AddIngredient(4941); // 哀木
            recipe.AddIngredient(4942); // 南瓜王
            recipe.AddIngredient(4943); // 冰雪女王
            recipe.AddIngredient(4944); // 常绿尖叫怪
            recipe.AddIngredient(4945); // 圣诞坦克
            recipe.AddIngredient(4946); // 暗黑魔法师
            recipe.AddIngredient(4947); // 食人魔
            recipe.AddIngredient(4948); // 双足翼龙
            recipe.AddIngredient(4949); // 光之女皇
            recipe.AddIngredient(4950); // 史莱姆皇后
            recipe.AddIngredient(5110); // 独眼巨鹿

            recipe.AddTile(TileID.LunarCraftingStation);
            recipe.Register();
        }
    }
}
