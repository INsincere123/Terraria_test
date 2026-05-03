using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Items.Accessories.Effects;
using TestMod.Common.Systems;
using TestMod.Buffs;
using TestMod.Rarities;
using TestMod.Items.Accessories.Dashes;
using System.Collections.Generic;

namespace TestMod.Items.Accessories
{
    // ============================================================================
    //  OmniGuardianAccessory  ——  综合守护饰品 (天界星盘风格翅膀)
    // ----------------------------------------------------------------------------
    //  这个饰品现在是 "Effects 模块组合" 的演示:
    //   - 翅膀飞行系统          —— 继承 OmniWingItem 自动获得
    //   - 攻击属性              —— CombatStatsEffect
    //   - 防御 / HP / MP        —— DefensiveStatsEffect
    //   - 召唤栏 / 哨兵栏       —— SummonStatsEffect
    //   - 三重闪避              —— TripleDodgeEffect
    //   - 综合生存              —— SurvivalEffect
    //   - 全套 debuff 免疫      —— DebuffImmunityEffect
    //   - 地面移动 + 鞋子       —— MoveSpeedEffect
    //   - 快速下落              —— FastFallEffect
    //   - 自定义冲刺            —— CustomDashEffect
    //   - 药水增强              —— PotionEffect
    //   - 常驻 buff             —— BuffApplyEffect
    //
    //  数值在下方"调节区"内集中调整, 想要做"轻量翅膀 / 仅生存护符"等组合,
    //  只需新建饰品, 选择性调用其中几个模块即可。
    //
    //  贴图:
    //    - OmniGuardianAccessory.png
    //    - OmniGuardianAccessory_Wings.png
    // ============================================================================

    [AutoloadEquip(EquipType.Wings)]
    public class OmniGuardianAccessory : OmniWingItem
    {
        // ========================================================================
        // =====                  数值调节区 (可自由修改)                     =====
        // ========================================================================

        // ---------- 翅膀飞行 ----------
        // 参考: vanilla 月光仙翼 1800 / 火神之翼 1500 / 女皇之翼 1800; 飞行时间单位是 tick (60 tick = 1秒)
        protected override WingFlightConfig WingsConfig => new WingFlightConfig {
            FlyTime              = 3600,   // 翅膀总飞行时间 tick (3600 = 60秒, 因开了 InfiniteFlight 实际无限)
            HorizontalSpeed      = 20f,    // 水平最大飞行速度 (vanilla 月光仙翼 9, 女皇之翼 9.5; 20 已经很离谱)
            HorizontalAccelMult  = 1.2f,   // 水平加速度倍率 (1.0 = 不加速, 越高起步越快)
            AscentWhenFalling    = 1.2f,   // 下落时按住跳跃的上升力 (vanilla 大多 0.85f, 越高越能"翻盘")
            AscentWhenRising     = 0.1f,   // 已经在上升时按住跳跃的额外加速度
            MaxCanAscendMult     = 0.8f,   // 当前 Y 速 / 最大上升速度 < 此倍率时才能继续加速 (越接近 1 越能持续顶上去)
            MaxAscentMult        = 3f,     // 最大上升速度倍率 (vanilla 大多 1.5; 3 = 翻倍上升)
            ConstantAscend       = 0.1f,   // 持续向上的恒定力 (越高越"飘", 越低越像滑翔)
            HoverHorizontalSpeed = 7.5f,   // 按下键悬浮时的水平速度 (vanilla 女皇之翼 6.25)
            HoverAccelMult       = 1.5f,   // 按下键悬浮时的水平加速度倍率
            UpBoostMultiplier    = 5f,     // 按上+跳跃 时的上升加速倍率 (1.0=禁用, vanilla 女皇之翼 1.5, 5 = 喷气背包级)
            InfiniteFlight       = true,   // empressBrooch 御翼徽章: 飞行不消耗 FlyTime
            EnablePerfectHover   = true,   // 按下+跳跃 视觉完全静止悬停 (内部 velocity.Y = -0.0001f, 不会卡虚空跑步 bug)
        };

        // ---------- 攻击属性 ----------
        // 作用职业默认 Generic (所有职业); 想做职业专属饰品请改 CombatStatsConfig.ClassType
        public const float DamageBonus       = 1.00f; // 伤害加成 (1.0f = +100% 翻倍, 0.1f = +10%)
        public const int   CritBonus         = 35;    // 暴击率加成 (单位是百分点, 35 = +35%)
        public const float AttackSpeedBonus  = 0.35f; // 攻速加成 (0.35f = +35% 攻速)
        public const int   ArmorPenetration  = 24;    // 穿甲值 (直接抵消怪物的对应防御)

        // ---------- 防御属性 ----------
        // MaxLife / MaxMana 基于 statLifeMax2 计算, 与其它 +最大生命 饰品按百分比正确叠加
        public const float MaxLifeBonus         = 1f;     // 最大生命加成 (1f = +100% 翻倍, 0.1f = +10%)
        public const float MaxManaBonus         = 1f;     // 最大法力加成 (同上)
        public const float DamageReductionBonus = 0.15f;  // 免伤百分比 (0.15f = -15% 受伤; vanilla 上限约 0.5)
        public const int   DefenseBonus         = 15;     // 固定防御加成 (每点 ≈ -0.5 受伤)
        public const int   LifeRegenPerSec      = 15;     // 基础生命再生 HP/秒 (人类直觉单位, 模块内部自动 ×2 转 vanilla 1/2 HP/s)
        public const float ManaRegenBonus       = 1.0f;   // 法力恢复倍率 (1.0f = +100% 恢复速度, 模块内部自动 ×100)

        // ---------- 召唤属性 ----------
        public const int   ExtraMinionSlots = 8;  // 额外召唤栏位 (vanilla 默认仅 1, 8 = 共 9 个仆从)
        public const int   ExtraSentrySlots = 3;  // 额外哨兵栏位 (vanilla 默认 1)

        // ---------- 移动 ----------
        public const float MoveSpeedBonus = 0.10f;  // 地面移速加成 (0.10f = +10%)
        public const float RunSpeedCap    = 18.0f;  // 奔跑速度上限 (vanilla 默认 6.0, 火神靴 9.0; 18 = 三倍火神靴)

        // ---------- 药水 ----------
        // 治疗最终值 = (原始治疗 + Flat) × Mult; 例: 大治疗药水原 150 -> (150+50)×5 = 1000
        public const int   PotionHealFlatBonus = 50; // 固定加值 HP (在乘法前应用; 0 = 不加成)
        public const float PotionHealMultBonus = 5f; // 治疗倍率 (1f = 不变, 2f = 翻倍, 5f = 五倍)

        // ---------- 生存 ----------
        public const float LowHpDamageReduction     = 0.35f; // HP < 50% 时叠加的额外免伤 (0.35f = +35%)
        public const int   DebuffDefensePerStack    = 20;    // 身上每个 debuff 增加的防御 (越多 debuff 越铁)
        public const int   DebuffRegenPerStack      = 10;    // 身上每个 debuff 增加的再生 (单位 1/2 HP/s, 10 = +5 HP/s)
        public const int   LostHpRegenMin           = 10;    // 满血时的最低再生 HP/s
        public const int   LostHpRegenMax           = 100;   // 0 血时的最高再生 HP/s (按当前血量百分比线性插值)
        public const int   ExtraDebuffTimeReduction = 2;     // 每帧额外减少的 debuff tick 数 (1 = 减半衰减, 2 = 三分之一时长, 越大消得越快)
        public const int   ExtraImmuneFrames        = 90;    // 受伤后追加的无敌帧 tick (vanilla 默认 ~30, 加 10 = 多 ~17%)

        // ---------- 快速下落 ----------
        // 触发条件: 按住下键 + 不按跳跃 + 正在下落 (与翅膀悬浮条件互斥, 不会冲突)
        public const float ExtraFallSpeed   = 30f; // 按下键时的最大下落速度 (vanilla 默认 maxFallSpeed = 10f)
        public const float FallGravityBoost = 2f;  // 按下键时的重力倍率 (1.0f = 不加速, 2.0f = 立即顶到上限)

        // ---------- 闪避 ----------
        // 三重闪避: 神圣套(100%/30s, vanilla自动管理) + 黑带(10%几率, 永久) + 自定义额外闪避(下方)
        public const int   ExtraDodgeChanceDenominator = 10;     // 自定义闪避几率分母 (10 = 1/10 = 10%; 设 0 禁用; 越大概率越低)
        public const int   ExtraDodgeCooldownTicks     = 60 * 15; // 自定义闪避触发后的冷却 (60 tick = 1秒, 这里 15 秒)

        // ========================================================================

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.value = Item.sellPrice(platinum: 100);
            Item.rare  = ModContent.RarityType<AntaresRarity>();
            if (CalamityCompatSystem.CalamityLoaded)
                Item.rare = CalamityCompatSystem.CalamityRarity;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // [0] 应用翅膀飞行 + 完美悬浮 (基类提供)
            base.UpdateAccessory(player, hideVisual);

            // [1] 攻击属性
            CombatStatsEffect.Apply(player, new CombatStatsConfig {
                Damage           = DamageBonus,
                Crit             = CritBonus,
                AttackSpeed      = AttackSpeedBonus,
                ArmorPenetration = ArmorPenetration,
            });

            // [2] 防御 / HP / MP
            DefensiveStatsEffect.Apply(player, new DefensiveStatsConfig {
                MaxLifeBonus    = MaxLifeBonus,
                MaxManaBonus    = MaxManaBonus,
                DamageReduction = DamageReductionBonus,
                Defense         = DefenseBonus,
                LifeRegenPerSec = LifeRegenPerSec,
                ManaRegenMult   = ManaRegenBonus,
            });

            // [3] 召唤栏 / 哨兵栏
            SummonStatsEffect.Apply(player, ExtraMinionSlots, ExtraSentrySlots);

            // [4] 移动 + 鞋子
            MoveSpeedEffect.Apply(player, new MoveSpeedConfig {
                MoveSpeed           = MoveSpeedBonus,
                RunSpeedCap         = RunSpeedCap,
                IceSkate            = true,
                WaterWalk           = true,
                FireBlockImmune     = true,
                LavaImmune          = true,
                LavaImmuneTimeBonus = 420,
                NoKnockback         = true,
                LongInvince         = true,
            });

            // [5] 快速下落
            FastFallEffect.Apply(player, ExtraFallSpeed, FallGravityBoost);

            // [6] 三重闪避
            TripleDodgeEffect.Apply(player, new TripleDodgeConfig {
                EnableHallowedShadow   = true,
                EnableBlackBelt        = true,
                EnableExtra            = true,
                ExtraChanceDenominator = ExtraDodgeChanceDenominator,
                ExtraCooldownTicks     = ExtraDodgeCooldownTicks,
            });

            // [7] 综合生存
            SurvivalEffect.Apply(player, new SurvivalConfig {
                EnableLowHpReduction    = true,
                LowHpDamageReduction    = LowHpDamageReduction,
                EnableDebuffStack       = true,
                DebuffDefensePerStack   = DebuffDefensePerStack,
                DebuffRegenPerStack     = DebuffRegenPerStack,
                EnableLostHpRegen       = true,
                LostHpRegenMin          = LostHpRegenMin,
                LostHpRegenMax          = LostHpRegenMax,
                EnableDebuffDecay       = true,
                DebuffTimeReduction     = ExtraDebuffTimeReduction,
                EnableExtraImmuneFrames = true,
                ExtraImmuneFrames       = ExtraImmuneFrames,
            });

            // [8] 药水
            PotionEffect.Apply(player, new PotionConfig {
                EnablePhilosophersStone = true,
                HealFlatBonus           = PotionHealFlatBonus,
                HealMultBonus           = PotionHealMultBonus,
            });

            // [9] 全套 debuff 免疫
            DebuffImmunityEffect.ApplyAll(player);

            // [10] 常驻 buff
            player.AddBuff(BuffID.Honey, 2);                                  // 蜂蜜 buff
            player.AddBuff(ModContent.BuffType<GravityNormalizerBuff>(), 2);  // 重力正常化 buff
            player.AddBuff(BuffID.WellFed3, 2);
            player.AddBuff(BuffID.DryadsWard, 2);
            player.AddBuff(BuffID.NebulaUpMana3, 2);

            // [11] 自定义冲刺 (灾厄风格)
            player.GetModPlayer<DashPlayer>().ActiveDashId  = "LongDash";   // 长冲刺 (V键)
            player.GetModPlayer<DashPlayer>().ActiveBlinkId = "ShortDash";  // 短冲刺 (C键)
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string longDashKey  = KeybindUtils.GetKeyText(OmniKeybinds.DashKey);
            string shortDashKey = KeybindUtils.GetKeyText(OmniKeybinds.BlinkKey);

            tooltips.Add(new TooltipLine(Mod, "OmniDesc",
                $"获得超级翅膀、奔跑效果\n" +
                $"获得冲刺效果 (长冲刺 [{longDashKey}] / 短冲刺 [{shortDashKey}])\n" +
                $"超坚硬躯体，超强生存能力，三重闪避\n" +
                $"全方位属性加成\n" +
                $"免疫所有原版 debuff\n" +
                $"不受太空低重力影响\n" +
                $"耐药性降低 25%，治疗效果提高\n" +
                $"泰拉大陆在你眼中都是蝼蚁，去挑战愤怒的突变体吧"));
        }

        public override void AddRecipes()
        {
            Recipe recipe = Recipe.Create(Type);

            // 添加所有大师模式圣物（Master Trophies）
            recipe.AddIngredient(ItemID.EyeofCthulhuMasterTrophy); // 克苏鲁之眼
            recipe.AddIngredient(ItemID.EaterofWorldsMasterTrophy); // 世界吞噬怪
            recipe.AddIngredient(ItemID.BrainofCthulhuMasterTrophy); // 克苏鲁之脑
            recipe.AddIngredient(ItemID.SkeletronMasterTrophy); // 骷髅王
            recipe.AddIngredient(ItemID.QueenBeeMasterTrophy); // 蜂王
            recipe.AddIngredient(ItemID.KingSlimeMasterTrophy); // 史莱姆王
            recipe.AddIngredient(ItemID.WallofFleshMasterTrophy); // 血肉墙
            recipe.AddIngredient(ItemID.TwinsMasterTrophy); // 双子魔眼
            recipe.AddIngredient(ItemID.DestroyerMasterTrophy); // 毁灭者
            recipe.AddIngredient(ItemID.SkeletronPrimeMasterTrophy); // 机械骷髅王
            recipe.AddIngredient(ItemID.PlanteraMasterTrophy); // 世纪之花
            recipe.AddIngredient(ItemID.GolemMasterTrophy); // 石巨人
            recipe.AddIngredient(ItemID.DukeFishronMasterTrophy); // 猪龙鱼公爵
            recipe.AddIngredient(ItemID.LunaticCultistMasterTrophy); // 拜月教邪教徒
            recipe.AddIngredient(ItemID.MoonLordMasterTrophy); // 月亮领主
            recipe.AddIngredient(ItemID.UFOMasterTrophy); // 火星飞碟
            recipe.AddIngredient(ItemID.FlyingDutchmanMasterTrophy); // 荷兰飞盗船
            recipe.AddIngredient(ItemID.MourningWoodMasterTrophy); // 哀木
            recipe.AddIngredient(ItemID.PumpkingMasterTrophy); // 南瓜王
            recipe.AddIngredient(ItemID.IceQueenMasterTrophy); // 冰雪女王
            recipe.AddIngredient(ItemID.EverscreamMasterTrophy); // 常绿尖叫怪
            recipe.AddIngredient(ItemID.SantankMasterTrophy); // 圣诞坦克
            recipe.AddIngredient(ItemID.DarkMageMasterTrophy); // 暗黑魔法师
            recipe.AddIngredient(ItemID.OgreMasterTrophy); // 食人魔
            recipe.AddIngredient(ItemID.BetsyMasterTrophy); // 双足翼龙
            recipe.AddIngredient(ItemID.FairyQueenMasterTrophy); // 光之女皇
            recipe.AddIngredient(ItemID.QueenSlimeMasterTrophy); // 史莱姆皇后
            recipe.AddIngredient(ItemID.DeerclopsMasterTrophy); // 独眼巨鹿

            recipe.AddTile(TileID.LunarCraftingStation);
            recipe.Register();
        }
    }
}