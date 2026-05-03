using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 魔法类（魔杖、法术书等）
    /// 魔法专属 manaMult；要求 mana > 3（原版机制：法力消耗太低时 manaMult 无效）
    /// 明确排除召唤武器（召唤武器共用 Magic 前缀池，必须在 CanRoll 里隔离）
    /// </summary>
    public class RefinementMagicPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================

        public const float DamageMult         = 1.66f;  // 伤害倍率
        public const float KnockbackMult      = 1.10f;  // 击退倍率
        public const float UseTimeMult        = 0.80f;  // 使用时间倍率（越小越快）
        public const float ManaMult           = 0.70f;  // 魔力消耗倍率，0.70f = -30%（魔法专属）
        public const int   CritBonus          = 20;      // 暴击率加成（%）
        public const int   ArmorPenetration   = 10;      // 护甲穿透

        public const float ReforgeValueMult   = 20.0f;

        // ============================================================

        // 借用 Magic 前缀池（召唤武器也在此池，靠 CanRoll 隔离）
        public override PrefixCategory Category => PrefixCategory.Magic;

        public override bool CanRoll(Item item)
            // 魔法武器 且 mana > 3 且 不是召唤武器（防止串入召唤池）
            => item.CountsAsClass(DamageClass.Magic)
            && !item.CountsAsClass(DamageClass.Summon)
            && item.mana > 3;

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
            knockbackMult = KnockbackMult;
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
