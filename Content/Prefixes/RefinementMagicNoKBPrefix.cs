using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 魔法无击退类
    /// 与 RefinementMagicPrefix 互斥：仅适用于 knockBack == 0 的魔法武器
    /// 无 knockbackMult，避免原版验证拒绝前缀
    /// </summary>
    public class RefinementMagicNoKBPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================

        public const float DamageMult         = 1.35f;  // 伤害倍率
        public const float UseTimeMult        = 0.80f;  // 使用时间倍率（越小越快）
        public const float ManaMult           = 0.77f;  // 魔力消耗倍率，0.77f = -23%（魔法专属）
        public const int   CritBonus          = 20;     // 暴击率加成（%）
        public const int   ArmorPenetration   = 10;     // 护甲穿透

        public const float ReforgeValueMult   = 20.0f;

        // ============================================================

        public override PrefixCategory Category => PrefixCategory.Magic;

        public override bool CanRoll(Item item)
            // 魔法武器 且 mana > 3 且 不是召唤武器 且 无击退
            => item.CountsAsClass(DamageClass.Magic)
            && !item.CountsAsClass(DamageClass.Summon)
            && item.mana > 3
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
            damageMult    = DamageMult;
            // knockbackMult 不赋值：无击退武器原版验证会拒绝整个前缀
            useTimeMult   = UseTimeMult;
            manaMult      = ManaMult;
            critBonus     = CritBonus;
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
