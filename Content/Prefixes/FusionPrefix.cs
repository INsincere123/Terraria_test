using System.Collections.Generic;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Systems;

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
        public const int DefenseBonus = 2;              // 防御值（整数）
        public const float DamageReduction = 0.01f;     // 伤害减免，0.01f = 1%
        public const int MaxLifeBonus = 10;             // 最大生命值（整数）
        public const int LifeRegenBonus = 1;            // 生命回复（2 = 每秒 +1 HP）

        // ---------- 伤害类 ----------
        public const float DamageBonus = 0.025f;         // 全伤害加成，0.04f = 4%
        public const int CritChanceBonus = 2;           // 全暴击率加成（整数，单位：%）
        public const int ArmorPenetrationBonus = 2;     // 护甲穿透（整数）

        // ---------- 速度类 ----------
        public const float MoveSpeedBonus = 0.03f;      // 移动速度加成，0.04f = 4%
        public const float MeleeSpeedBonus = 0.02f;     // 近战攻速加成，0.04f = 4%

        // ---------- 魔法类 ----------
        public const int MaxManaBonus = 10;             // 最大魔力加成（整数）
        public const float ManaCostReduction = 0.02f;   // 魔力消耗减少，0.03f = 3%

        // ---------- 幸运 ----------
        public const float LuckBonus = 0.1f;            // 幸运值加成

        // ---------- 重铸价值倍率 ----------
        // 1.0f = 不变，1.05f ≈ 稀有度+1，1.2f ≈ 稀有度+2
        public const float ReforgeValueMult = 8.0f;

        // ============================================================
        // =====================【实现 - 一般无需改动】==================
        // ============================================================

        public override PrefixCategory Category => PrefixCategory.Accessory;
        public override bool IsLoadingEnabled(Mod mod) => PrefixAvailabilitySystem.FusionEnabled;
        // 加载后允许应用到饰品；关闭时本类型不注册。
        public override bool CanRoll(Item item) => true;

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
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.Defense", DefenseBonus)) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionDR",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.DR", (DamageReduction * 100))) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionMaxLife",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.MaxLife", MaxLifeBonus)) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionLifeRegen",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.LifeRegen", (LifeRegenBonus / 2.0f))) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionDamage",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.Damage", (DamageBonus * 100))) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionCrit",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.Crit", CritChanceBonus)) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionArmorPen",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.ArmorPenetration", ArmorPenetrationBonus)) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionMoveSpeed",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.MoveSpeed", (MoveSpeedBonus * 100))) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionMeleeSpeed",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.MeleeSpeed", (MeleeSpeedBonus * 100))) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionMaxMana",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.MaxMana", MaxManaBonus)) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionManaCost",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.ManaCost", (ManaCostReduction * 100))) { IsModifier = true };

            yield return new TooltipLine(Mod, "TestMod:FusionLuck",
                Language.GetTextValue("Mods.TestMod.PrefixTooltips.Luck", LuckBonus)) { IsModifier = true };
        }
    }
}
