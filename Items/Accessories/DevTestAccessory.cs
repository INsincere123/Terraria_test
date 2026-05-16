using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Items.Accessories.Effects;

namespace TestMod.Items.Accessories
{
    // ============================================================================
    //  开发者测试饰品  ——  测试 StatConversionEffect 的全部来源/目标/曲线组合
    // ----------------------------------------------------------------------------
    //  不设合成配方，直接 /item DevTestAccessory 获取。
    //  占位贴图：原版盾牌图标，替换为自定义 PNG 后删除 Texture override。
    //
    //  修改数值：只动下方数值调节区的常量即可，不需要理解框架内部。
    // ============================================================================
    public class DevTestAccessory : ModItem
    {
        //public override string Texture => "Terraria/Images/Item_" + ItemID.EoCShield;

        // ── 数值调节区 ──────────────────────────────────────────────────────────
        // 曲线指数：2f = 先慢后快（肉装堆得越多增幅越显著）
        //           0.5f = 先快后慢（低数值时就有明显加成，随后趋于平缓）
        private const float Steep  = 2f;
        private const float Smooth = 0.5f;

        // 防御力 → 全属性伤害（0~200 防御 → 0~50% 全属性伤害）
        private const float DefMin = 0f, DefMax = 200f, DefDmg = 0.5f;

        // 最大生命 → 暴击伤害（100~2000 HP → 0~+0.5 critDamageBonus）
        private const float MhpMin = 100f, MhpMax = 2000f, MhpCritDmg = 0.5f;

        // 当前生命 → 全属性暴击率（100~2000 HP → 0~+10% 暴击）
        private const float ChpMin = 100f, ChpMax = 2000f, ChpCrit = 10f;

        // 缺失生命 → 独立增伤（0~1000 缺失 → 0~+20% 独立增伤）
        private const float MissMin = 0f, MissMax = 1000f, MissDmg = 0.2f;

        // 减伤率 → 敌方伤害加深（0~0.3 减伤 → 0~+15% 加深）
        private const float EndMin = 0f, EndMax = 0.3f, EndVuln = 0.15f;

        // 生命回复 → 近战攻速（0~20 回复 → 0~+15% 近战攻速）
        private const float RegenMin = 0f, RegenMax = 20f, RegenSpd = 0.15f;

        // 最大魔力 → 魔法伤害（20~400 魔力 → 0~+30% 魔法伤害）
        private const float ManaMin = 20f, ManaMax = 400f, ManaDmg = 0.3f;

        // 最大魔力 → 魔力消耗减少（20~400 魔力 → 0~-20% 消耗）
        private const float ManaCost = 0.2f;

        // 当前护盾 → 护甲穿透（0~300 护盾 → 0~+20 穿透）
        private const float ShMin = 0f, ShMax = 300f, ShAP = 20f;

        // 最大护盾 → 召唤伤害（0~300 最大护盾 → 0~+15% 召唤伤害）
        private const float ShMaxDmg = 0.15f;

        // 防御力 → 召唤栏位（0~200 防御 → 0~3 召唤槽，四舍五入）
        private const float DefMinionScale = 3f;

        // 最大生命 → 哨兵栏位（100~2000 HP → 0~2 哨兵槽，四舍五入）
        private const float MhpTurretScale = 2f;
        // ────────────────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.width     = 24;
            Item.height    = 24;
            Item.accessory = true;
            Item.rare      = ItemRarityID.Red;
            Item.value     = 0;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 防御力 → 全属性伤害（先慢后快）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.Defense,   SourceMin = DefMin,   SourceMax = DefMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = DefDmg,
                Target = TargetStatType.Damage,    TargetClass = DamageClass.Generic,
            });

            // 最大生命 → 暴击伤害（先慢后快）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.MaxHP,     SourceMin = MhpMin,   SourceMax = MhpMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = MhpCritDmg,
                Target = TargetStatType.CritDamage,
            });

            // 当前生命 → 暴击率（先慢后快）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.CurrentHP, SourceMin = ChpMin,   SourceMax = ChpMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = ChpCrit,
                Target = TargetStatType.CritChance, TargetClass = DamageClass.Generic,
            });

            // 缺失生命 → 独立增伤（先快后慢，越快死越暴）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.MissingHP, SourceMin = MissMin,  SourceMax = MissMax,
                Curve  = ConversionCurve.Power,    CurveParam = Smooth,
                OutputScale = MissDmg,
                Target = TargetStatType.IndependentDamage, TargetClass = DamageClass.Generic,
            });

            // 减伤率 → 敌方伤害加深（先慢后快）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.Endurance, SourceMin = EndMin,   SourceMax = EndMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = EndVuln,
                Target = TargetStatType.TargetVulnerability,
            });

            // 生命回复 → 近战攻速（先慢后快）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.LifeRegen, SourceMin = RegenMin, SourceMax = RegenMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = RegenSpd,
                Target = TargetStatType.AttackSpeed, TargetClass = DamageClass.Melee,
            });

            // 最大魔力 → 魔法伤害（先慢后快）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.MaxMana,   SourceMin = ManaMin,  SourceMax = ManaMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = ManaDmg,
                Target = TargetStatType.Damage,    TargetClass = DamageClass.Magic,
            });

            // 最大魔力 → 魔力消耗减少（共用来源，先慢后快）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.MaxMana,   SourceMin = ManaMin,  SourceMax = ManaMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = ManaCost,
                Target = TargetStatType.ManaCostReduction,
            });

            // 当前护盾 → 护甲穿透（先慢后快）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.ShieldCurrent, SourceMin = ShMin, SourceMax = ShMax,
                Curve  = ConversionCurve.Power,        CurveParam = Steep,
                OutputScale = ShAP,
                Target = TargetStatType.ArmorPenetration, TargetClass = DamageClass.Generic,
            });

            // 最大护盾 → 召唤伤害（先慢后快）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.ShieldMax, SourceMin = ShMin,    SourceMax = ShMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = ShMaxDmg,
                Target = TargetStatType.Damage,    TargetClass = DamageClass.Summon,
            });

            // 防御力 → 召唤栏位（先慢后快，四舍五入）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.Defense,   SourceMin = DefMin,   SourceMax = DefMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = DefMinionScale,
                Target = TargetStatType.MaxMinions,
            });

            // 最大生命 → 哨兵栏位（先慢后快，四舍五入）
            StatConversionPlayer.AddRule(player, new ConversionRule
            {
                Source = SourceStatType.MaxHP,     SourceMin = MhpMin,   SourceMax = MhpMax,
                Curve  = ConversionCurve.Power,    CurveParam = Steep,
                OutputScale = MhpTurretScale,
                Target = TargetStatType.MaxTurrets,
            });
        }
    }
}
