using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 远程类（弓、枪、发射器）
    /// 远程专属 shootSpeedMult（投射物初速度）
    /// </summary>
    public class RefinementRangedPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================

        public const float DamageMult         = 1.66f;  // 伤害倍率
        public const float KnockbackMult      = 1.10f;  // 击退倍率
        public const float UseTimeMult        = 0.80f;  // 使用时间倍率（越小越快）
        public const float ShootSpeedMult     = 6.0f;  // 投射物初速度倍率（远程专属）
        public const int   CritBonus          = 20;     // 暴击率加成（%）
        public const int   ArmorPenetration   = 10;     // 护甲穿透

        public const float ReforgeValueMult   = 20.0f;

        // ============================================================

        public override PrefixCategory Category => PrefixCategory.Ranged;

        public override bool CanRoll(Item item)
            => item.CountsAsClass(DamageClass.Ranged);

        public override void SetStats(
            ref float damageMult,
            ref float knockbackMult,
            ref float useTimeMult,
            ref float scaleMult,
            ref float shootSpeedMult,
            ref float manaMult,
            ref int   critBonus)
        {
            damageMult     = DamageMult;
            knockbackMult  = KnockbackMult;
            useTimeMult    = UseTimeMult;
            shootSpeedMult = ShootSpeedMult;
            critBonus      = CritBonus;
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
