using Terraria;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  DefensiveStatsEffect  ——  防御 / 生命 / 法力 属性加成
    // ----------------------------------------------------------------------------
    //  使用示例:
    //
    //    DefensiveStatsEffect.Apply(player, new DefensiveStatsConfig {
    //        MaxLifeBonus      = 1f,    // +100% 最大生命
    //        MaxManaBonus      = 1f,    // +100% 最大法力
    //        DamageReduction   = 0.15f, // +15% 免伤
    //        Defense           = 15,    // +15 防御
    //        LifeRegenPerSec   = 15,    // +15 HP/s 基础再生
    //        ManaRegenMult     = 1.0f,  // +1.0 倍法力恢复
    //    });
    //
    //  注意:
    //   - LifeRegenPerSec 单位是 HP/秒 (内部会自动 ×2 转换为 vanilla 的 1/2 HP/s)
    //   - MaxLifeBonus 基于 statLifeMax2 计算, 与其它 +最大生命 饰品正确叠加
    // ============================================================================

    public struct DefensiveStatsConfig
    {
        public float MaxLifeBonus;      // 1.0f = +100%
        public float MaxManaBonus;      // 1.0f = +100%
        public float DamageReduction;   // 0.15f = +15% 免伤
        public int   Defense;
        public int   LifeRegenPerSec;   // 单位 HP/s (输入时人类直觉, 内部 ×2)
        public float ManaRegenMult;     // 1.0f = +1.0 倍法力恢复
    }

    public static class DefensiveStatsEffect
    {
        public static void Apply(Player player, DefensiveStatsConfig cfg)
        {
            if (cfg.MaxLifeBonus != 0f)
                player.statLifeMax2 += (int)(player.statLifeMax2 * cfg.MaxLifeBonus);

            if (cfg.MaxManaBonus != 0f)
                player.statManaMax2 += (int)(player.statManaMax2 * cfg.MaxManaBonus);

            if (cfg.DamageReduction != 0f)
                player.endurance += cfg.DamageReduction;

            if (cfg.Defense != 0)
                player.statDefense += cfg.Defense;

            if (cfg.LifeRegenPerSec != 0)
                player.lifeRegen += cfg.LifeRegenPerSec * 2; // 转换为 1/2 HP/s 单位

            if (cfg.ManaRegenMult != 0f)
                player.manaRegenBonus += (int)(cfg.ManaRegenMult * 100); // 单位是百分比 ×100
        }
    }
}
