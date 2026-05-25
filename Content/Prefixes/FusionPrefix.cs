using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Prefixes
{
    /// <summary>
    /// 通用全局饰品前缀"融合"
    /// 适用于所有可重铸的饰品，提供全方位属性提升
    /// </summary>
    public class FusionPrefix : ModPrefix
    {
        // ============================================================
        // ====================【可调参数 - 慢慢测试】===================
        // ============================================================
        // 不需要的属性：同时注释掉下方对应的 ApplyAccessoryEffects 行和 GetTooltipLines 行

        // ---------- 生存类 ----------
        public const int DefenseBonus = 4;              // 防御值（整数）
        public const float DamageReduction = 0.01f;     // 伤害减免，0.01f = 1%
        public const int MaxLifeBonus = 15;             // 最大生命值（整数）
        public const int LifeRegenBonus = 2;            // 生命回复（2 = 每秒 +1 HP）

        // ---------- 伤害类 ----------
        public const float DamageBonus = 0.04f;         // 全伤害加成，0.04f = 4%
        public const int CritChanceBonus = 4;           // 全暴击率加成（整数，单位：%）
        public const int ArmorPenetrationBonus = 2;     // 护甲穿透（整数）

        // ---------- 速度类 ----------
        public const float MoveSpeedBonus = 0.04f;      // 移动速度加成，0.04f = 4%
        public const float MeleeSpeedBonus = 0.04f;     // 近战攻速加成，0.04f = 4%

        // ---------- 魔法类 ----------
        public const int MaxManaBonus = 10;             // 最大魔力加成（整数）
        public const float ManaCostReduction = 0.03f;   // 魔力消耗减少，0.03f = 3%

        // ---------- 幸运 ----------
        public const float LuckBonus = 0.1f;            // 幸运值加成

        // ---------- 重铸价值倍率 ----------
        // 1.0f = 不变，1.05f ≈ 稀有度+1，1.2f ≈ 稀有度+2
        public const float ReforgeValueMult = 10.0f;

        // ============================================================
        // =====================【实现 - 一般无需改动】==================
        // ============================================================

        public override PrefixCategory Category => PrefixCategory.Accessory;

        public override void ApplyAccessoryEffects(Player player)
        {
            // 生存类
            player.statDefense += DefenseBonus;
            player.endurance += DamageReduction;
            player.statLifeMax2 += MaxLifeBonus;
            player.lifeRegen += LifeRegenBonus;

            // 伤害类
            player.GetDamage(DamageClass.Generic) += DamageBonus;
            player.GetCritChance(DamageClass.Generic) += CritChanceBonus;
            player.GetArmorPenetration(DamageClass.Generic) += ArmorPenetrationBonus;

            // 速度类
            player.moveSpeed += MoveSpeedBonus;
            player.GetAttackSpeed(DamageClass.Melee) += MeleeSpeedBonus;

            // 魔法类
            player.statManaMax2 += MaxManaBonus;
            player.manaCost -= ManaCostReduction;

            // 幸运
            player.luck += LuckBonus;
        }

        public override void ModifyValue(ref float valueMult)
        {
            valueMult = ReforgeValueMult;
        }

        public override IEnumerable<TooltipLine> GetTooltipLines(Item item)
        {
            // 每条 tooltip 对应上方一个参数，注释掉不需要的即可

            yield return new TooltipLine(Mod, "TestMod:FusionDefense",
                $"+{DefenseBonus} 防御") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionDR",
                $"+{(DamageReduction * 100):0.#}% 伤害减免") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionMaxLife",
                $"+{MaxLifeBonus} 最大生命值") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionLifeRegen",
                $"+{(LifeRegenBonus / 2.0f):0.#} 每秒生命回复") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionDamage",
                $"+{(DamageBonus * 100):0.#}% 全伤害") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionCrit",
                $"+{CritChanceBonus}% 暴击率") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionArmorPen",
                $"+{ArmorPenetrationBonus} 护甲穿透") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionMoveSpeed",
                $"+{(MoveSpeedBonus * 100):0.#}% 移动速度") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionMeleeSpeed",
                $"+{(MeleeSpeedBonus * 100):0.#}% 近战攻速") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionMaxMana",
                $"+{MaxManaBonus} 最大魔力") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionManaCost",
                $"-{(ManaCostReduction * 100):0.#}% 魔力消耗") { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionLuck",
                $"+{LuckBonus:0.##} 幸运") { IsModifier = true };
        }
    }
}
