using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 召唤无击退类（如刃杖等无击退召唤武器）
    /// 与 RefinementSummonPrefix 互斥：仅适用于 knockBack == 0 的召唤武器
    /// 无 knockbackMult，避免原版验证拒绝前缀
    /// </summary>
    public class RefinementSummonNoKBPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================

        public const float DamageMult         = 1.66f;  // 伤害倍率
        public const float UseTimeMult        = 0.30f;  // 使用时间倍率（召唤只用一次，影响召唤动画速度）
        public const int   ArmorPenetration   = 20;     // 护甲穿透（1.4.5 召唤专属属性）

        public const float ReforgeValueMult   = 20.0f;

        // ============================================================

        public override PrefixCategory Category => PrefixCategory.Magic;

        public override bool CanRoll(Item item)
            // 召唤武器 且 不是鞭子 且 无击退
            => item.CountsAsClass(DamageClass.Summon)
            && !item.CountsAsClass(DamageClass.SummonMeleeSpeed)
            && item.knockBack == 0f;

        public override void SetStats(
            ref float damageMult,
            ref float knockbackMult,
            ref float useTimeMult,
            ref float scaleMult,
            ref float shootSpeedMult,
            ref float manaMult,
            ref int   critBonus)
        {
            damageMult  = DamageMult;
            // knockbackMult 不赋值：无击退武器原版验证会拒绝整个前缀
            useTimeMult = UseTimeMult;
            // critBonus 不赋值：召唤伤害不能暴击
            // manaMult  不赋值：召唤武器召唤后不持续消耗魔力
        }

        public override void Apply(Item item)
        {
            item.ArmorPenetration += ArmorPenetration;
        }

        public override void ModifyValue(ref float valueMult)
        {
            valueMult = ReforgeValueMult;
        }

        public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
        {
            yield return new TooltipLine(Mod, "TestMod:RefinementArmorPen",
                $"+{ArmorPenetration} 护甲穿透") { IsModifier = true };
        }
    }
}
