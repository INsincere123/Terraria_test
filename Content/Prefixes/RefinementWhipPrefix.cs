using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 鞭子类
    /// 鞭子走近战前缀池（SummonMeleeSpeed），有 scaleMult（影响鞭子范围）
    /// 鞭子造成召唤伤害，不能暴击，无 critBonus / manaMult / shootSpeedMult
    /// </summary>
    public class RefinementWhipPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================

        public const float DamageMult         = 1.35f;  // 伤害倍率
        public const float KnockbackMult      = 1.20f;  // 击退倍率
        public const float UseTimeMult        = 0.80f;  // 使用时间倍率（越小挥鞭越快）
        public const float ScaleMult          = 1.60f;  // 大小倍率（影响鞭子攻击范围）
        public const int   ArmorPenetration   = 20;     // 护甲穿透

        public const float ReforgeValueMult   = 20.0f;

        // ============================================================

        // 鞭子走近战前缀池
        public override PrefixCategory Category => PrefixCategory.Melee;

        public override bool CanRoll(Item item)
            // 鞭子的伤害类型是 SummonMeleeSpeed
            => item.CountsAsClass(DamageClass.SummonMeleeSpeed);

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
            scaleMult     = ScaleMult;
            // critBonus 不赋值：召唤伤害不能暴击
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
