using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 召唤非鞭类（召唤法杖、哨兵）
    /// 召唤武器不能暴击，不消耗持续魔力，无 critBonus / manaMult / scaleMult / shootSpeedMult
    /// Category 借用 Magic 池（原版机制），靠 CanRoll 排除魔法和鞭子
    /// </summary>
    public class RefinementSummonPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================

        public const float DamageMult         = 1.35f;  // 伤害倍率
        public const float KnockbackMult      = 1.15f;  // 击退倍率
        public const float UseTimeMult        = 0.30f;  // 使用时间倍率（召唤只用一次，影响召唤动画速度）
        public const int   ArmorPenetration   = 10;     // 护甲穿透（1.4.5 召唤专属属性）

        public const float ReforgeValueMult   = 20.0f;

        // ============================================================

        // 借用 Magic 前缀池（与召唤武器共用，靠 CanRoll 隔离魔法武器）
        public override PrefixCategory Category => PrefixCategory.Magic;

        public override bool CanRoll(Item item)
            // 召唤武器 且 不是鞭子 且 有击退（无击退版本由 RefinementSummonNoKBPrefix 处理）
            => item.CountsAsClass(DamageClass.Summon)
            && !item.CountsAsClass(DamageClass.SummonMeleeSpeed)
            && item.knockBack > 0f;

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
