using Terraria;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  SummonStatsEffect  ——  召唤栏 / 哨兵栏加成
    // ----------------------------------------------------------------------------
    //  使用示例:
    //
    //    SummonStatsEffect.Apply(player, minionSlots: 8, sentrySlots: 3);
    //
    //  注意:
    //   - 召唤伤害/暴击等请走 CombatStatsEffect 并指定 ClassType = DamageClass.Summon
    //   - 此模块仅处理"槽位"
    // ============================================================================

    public static class SummonStatsEffect
    {
        public static void Apply(Player player, int minionSlots = 0, int sentrySlots = 0)
        {
            if (minionSlots != 0) player.maxMinions += minionSlots;
            if (sentrySlots != 0) player.maxTurrets += sentrySlots;
        }
    }
}
