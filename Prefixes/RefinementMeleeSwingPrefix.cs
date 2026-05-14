using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Prefixes
{
    /// <summary>
    /// 武器前缀"炼化" —— 近战挥动类（剑、锤、镐、斧、短剑）
    /// 有挥动动画的近战武器，支持 scaleMult
    /// </summary>
    public class RefinementMeleeSwingPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================
        // 乘数：1.0f = 不变；>1 = 增加；<1 = 减少
        // useTimeMult 反向：0.9f = 快 10%（数值越小越快）
        // 不需要的参数：注释掉 SetStats/Apply/GetTooltipLines 里对应行

        public const float DamageMult         = 1.35f;  // 伤害倍率
        public const float KnockbackMult      = 1.15f;  // 击退倍率
        public const float UseTimeMult        = 0.80f;  // 使用时间倍率（越小越快）
        public const float ScaleMult          = 1.50f;  // 武器大小倍率（影响挥动范围）
        public const int   CritBonus          = 20;     // 暴击率加成（%）
        public const int   ArmorPenetration   = 10;     // 护甲穿透

        public const float ReforgeValueMult   = 20.0f;   // 重铸价值倍率

        // ============================================================

        public override PrefixCategory Category => PrefixCategory.Melee;

        public override bool CanRoll(Item item)
            // 近战武器 且 有挥动动画（!noUseGraphic）
            => item.CountsAsClass(DamageClass.Melee) && !item.noUseGraphic;

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
            // damageMult / knockbackMult / useTimeMult / scaleMult / critBonus
            // 原版会自动显示，此处只补充额外属性
            yield return new TooltipLine(Mod, "TestMod:RefinementArmorPen",
                $"+{ArmorPenetration} 护甲穿透") { IsModifier = true };
        }
    }
}
