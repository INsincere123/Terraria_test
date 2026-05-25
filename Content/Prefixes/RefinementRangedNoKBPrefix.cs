using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 远程无击退类（部分弓、枪、发射器）
    /// 与 RefinementRangedPrefix 互斥：仅适用于 knockBack == 0 的远程武器
    /// 无 knockbackMult，避免原版验证拒绝前缀
    /// </summary>
    public class RefinementRangedNoKBPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================

        public const float DamageMult         = 1.35f;  // 伤害倍率
        public const float UseTimeMult        = 0.80f;  // 使用时间倍率（越小越快）
        public const float ShootSpeedMult     = 6.0f;   // 投射物初速度倍率（远程专属）
        public const int   CritBonus          = 20;     // 暴击率加成（%）
        public const int   ArmorPenetration   = 10;     // 护甲穿透

        public const float ReforgeValueMult   = 20.0f;

        // ============================================================

        public override PrefixCategory Category => PrefixCategory.Ranged;

        public override bool CanRoll(Item item)
            // 远程武器 且 无击退（与 RefinementRangedPrefix 互斥）
            => item.CountsAsClass(DamageClass.Ranged) && item.knockBack == 0f;

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
            // knockbackMult 不赋值：无击退武器原版验证会拒绝整个前缀
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
