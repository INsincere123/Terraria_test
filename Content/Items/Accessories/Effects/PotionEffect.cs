using Terraria;
using Terraria.ModLoader;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  PotionEffect  ——  药水相关增强
    // ----------------------------------------------------------------------------
    //  包含:
    //   [1] 哲学家之石效果 (pStone)  —— 耐药性 CD × 0.75 (缩短 25%)
    //   [2] 治疗加值                 —— 大治疗药水 +N HP
    //   [3] 治疗倍率                 —— 最终 = (原值 + Flat) × Mult
    //
    //  使用示例:
    //
    //    PotionEffect.Apply(player, new PotionConfig {
    //        EnablePhilosophersStone = true,
    //        HealFlatBonus           = 50,
    //        HealMultBonus           = 5f,
    //    });
    //
    //  注意: 治疗加值需要 ModItem 实现 ModifyHealLife / ModPlayer 实现 GetHealLife
    //        本模块通过 OmniEffectsPlayer 的 ModPlayer 钩子分发, 所以请放在装备时调用。
    //        不过 GetHealLife 钩子在 OmniEffectsPlayer 中尚未注册, 见下方注释。
    // ============================================================================

    public struct PotionConfig
    {
        public bool  EnablePhilosophersStone; // pStone (耐药性减 25%)
        public int   HealFlatBonus;           // +x HP 治疗加值
        public float HealMultBonus;           // 治疗倍率, 1.0f = 不加成
    }

    public static class PotionEffect
    {
        public static void Apply(Player player, PotionConfig cfg)
        {
            if (cfg.EnablePhilosophersStone)
                player.pStone = true;

            // 治疗加值通过 ModPlayer 字段传递给 GetHealLife 钩子
            var mp = player.GetModPlayer<OmniEffectsPlayer>();
            mp.Potion_HealFlatBonus = cfg.HealFlatBonus;
            mp.Potion_HealMultBonus = cfg.HealMultBonus;
        }

        // =====================================================================
        // 由 OmniEffectsPlayer.GetHealLife 调用
        // 先加固定值, 再乘倍率: 最终 = (原值 + FlatBonus) × MultBonus
        // =====================================================================
        public static void OnGetHealLife(OmniEffectsPlayer mp, ref int healValue)
        {
            if (mp.Potion_HealFlatBonus == 0 && mp.Potion_HealMultBonus == 0f) return;

            healValue += mp.Potion_HealFlatBonus;
            if (mp.Potion_HealMultBonus > 0f)
                healValue = (int)(healValue * mp.Potion_HealMultBonus);
        }
    }
}
