using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  CombatStatsEffect  ——  攻击属性加成
    // ----------------------------------------------------------------------------
    //  使用示例 (在饰品的 UpdateAccessory 中):
    //
    //    CombatStatsEffect.Apply(player, new CombatStatsConfig {
    //        Damage          = 1.00f,   // +100% 伤害
    //        Crit            = 35,      // +35% 暴击
    //        AttackSpeed     = 0.35f,   // +35% 攻速
    //        ArmorPenetration = 24,     // +24 穿甲
    //    });
    //
    //  默认 Generic 类 (作用于所有职业)。如需仅作用于某一职业, 修改 ClassType。
    // ============================================================================

    public struct CombatStatsConfig
    {
        public float Damage;            // +x% 伤害 (1.0f = +100%)
        public int   Crit;              // +x  暴击率 (单位是百分比)
        public float AttackSpeed;       // +x% 攻速
        public int   ArmorPenetration;  // +x  穿甲
        public DamageClass ClassType;   // 作用职业, 默认 Generic
    }

    public static class CombatStatsEffect
    {
        public static void Apply(Player player, CombatStatsConfig cfg)
        {
            DamageClass cls = cfg.ClassType ?? DamageClass.Generic;

            if (cfg.Damage != 0f)
                player.GetDamage(cls) += cfg.Damage;

            if (cfg.Crit != 0)
                player.GetCritChance(cls) += cfg.Crit;

            if (cfg.AttackSpeed != 0f)
                player.GetAttackSpeed(cls) += cfg.AttackSpeed;

            if (cfg.ArmorPenetration != 0)
                player.GetArmorPenetration(cls) += cfg.ArmorPenetration;
        }
    }
}
