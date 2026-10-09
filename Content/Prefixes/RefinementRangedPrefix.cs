using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Content.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 远程类（弓、枪、发射器）
    /// 远程弹幕运动倍率由 GlobalProjectile 处理，不重复修改初速度。
    /// </summary>
    public class RefinementRangedPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================

        public const float DamageMult         = 1.35f;  // 伤害倍率
        public const float KnockbackMult      = 1.15f;  // 击退倍率
        public const float UseTimeMult        = 0.80f;  // 使用时间倍率（越小越快）
        public const float ShootSpeedMult     = 2.0f;  // 独立飞行弹幕运动倍率
        public const int   CritBonus          = 20;     // 暴击率加成（%）
        public const int   ArmorPenetration   = 10;     // 护甲穿透

        public const float ReforgeValueMult   = 20.0f;

        // ============================================================

        public override PrefixCategory Category => PrefixCategory.Ranged;
        public override bool IsLoadingEnabled(Mod mod) => PrefixAvailabilitySystem.RefinementEnabled;

        public override bool CanRoll(Item item)
            // 有击退的远程武器（无击退版本由 RefinementRangedNoKBPrefix 处理）
            => item.CountsAsClass(DamageClass.Ranged) && item.knockBack > 0f;

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
            yield return new TooltipLine(Mod, "TestMod:RefinementProjectileSpeed",
                "+100%弹速") { IsModifier = true };
            yield return new TooltipLine(Mod, "TestMod:RefinementArmorPen",
                $"+{ArmorPenetration} 护甲穿透") { IsModifier = true };
        }
    }
}
