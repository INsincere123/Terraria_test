using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using TestMod.Items.DamageTypes;

namespace TestMod.Prefixes
{
    // ============================================================================
    //  炼化（RefinementPrefix） —— 专属于真实伤害武器的高阶词缀
    // ----------------------------------------------------------------------------
    //  名称显示在 Localization/en-US_Mods.TestMod.hjson 的 Prefix 节点下：
    //    Prefixes: { RefinementPrefix: { DisplayName: 炼化 } }
    //
    //  效果来源：
    //    SetStats  → 伤害/击退/攻速/暴击（引擎内置，tooltip 自动显示）
    //    Apply     → 护甲穿透（引擎无内置，需手动修改物品并在 GetTooltipLines 额外显示）
    //    ModifyValue → 重铸费用 ×20（高阶词缀应当昂贵）
    // ============================================================================
    public class RefinementPrefix : ModPrefix
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const float DamageMult         = 1.35f;  // 伤害倍率
        public const float KnockbackMult      = 1.15f;  // 击退倍率
        public const float UseTimeMult        = 0.70f;  // 使用时间倍率（< 1 = 加速）
        public const int   CritBonus          = 20;     // 暴击率加成（%）
        public const int   ArmorPenetration   = 10;     // 护甲穿透（固定）
        public const float ReforgeValueMult   = 20.0f;  // 重铸费用倍率
        // ─────────────────────────────────────────────────────────────

        // Custom：默认不出现在任何武器的重铸池，只由 TestGlobalItem.ChoosePrefix 手动控制
        // 防止炼化意外出现在原版近战/远程/魔法武器上
        public override PrefixCategory Category => PrefixCategory.Custom;

        // 只允许应用到真实伤害类型武器（非饰品、有伤害值）
        public override bool CanRoll(Item item)
            => item.DamageType == TrueDamageClass.Instance && item.damage > 0;

        // SetStats 处理引擎内置的乘法系数（伤害/击退/攻速/暴击）
        public override void SetStats(ref float damageMult, ref float knockbackMult,
            ref float useTimeMult, ref float scaleMult, ref float shootSpeedMult,
            ref float manaMult, ref int critBonus)
        {
            damageMult    = DamageMult;
            knockbackMult = KnockbackMult;
            useTimeMult   = UseTimeMult;
            critBonus     = CritBonus;
        }

        // Apply 处理 SetStats 覆盖不到的属性（护甲穿透）
        // 重铸时引擎会先重置物品到基础值，再依次调用 SetStats → Apply
        public override void Apply(Item item)
        {
            item.ArmorPenetration += ArmorPenetration;
        }

        // 重铸费用倍率：高阶词缀应让玩家三思
        public override void ModifyValue(ref float valueMult)
        {
            valueMult *= ReforgeValueMult;
        }

        // 护甲穿透在引擎 tooltip 里没有内置显示，手动追加一行
        public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
        {
            yield return new TooltipLine(Mod, "ArmorPen", $"+{ArmorPenetration} 护甲穿透")
            {
                IsModifier    = true,
                IsModifierBad = false,
            };
        }
    }
}
