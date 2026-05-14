using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 近战非挥动类（矛、链球、悠悠球等）
    /// 没有挥动动画（noUseGraphic = true），不支持 scaleMult
    /// useTimeMult 对悠悠球等无效，会被 ValidateItem 自动过滤，前缀仍可出现（只是少那条 stat）
    /// </summary>
    public class RefinementMeleeOtherPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================

        public const float DamageMult         = 1.35f;  // 伤害倍率
        public const float KnockbackMult      = 1.15f;  // 击退倍率
        public const float UseTimeMult        = 0.70f;  // 使用时间倍率（悠悠球等无效会被自动忽略）
        public const int   CritBonus          = 20;     // 暴击率加成（%）
        public const int   ArmorPenetration   = 10;     // 护甲穿透

        public const float ReforgeValueMult   = 20.0f;

        // ============================================================

        public override PrefixCategory Category => PrefixCategory.AnyWeapon;

        public override bool CanRoll(Item item)
            // 近战武器 且 无挥动动画（noUseGraphic），排除鞭子（SummonMeleeSpeed）
            => item.CountsAsClass(DamageClass.Melee)
            && item.noUseGraphic
            && !item.CountsAsClass(DamageClass.SummonMeleeSpeed);

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
            critBonus     = CritBonus;
            // scaleMult 不赋值：非挥动武器无效
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
