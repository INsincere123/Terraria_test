using Terraria;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  BuffApplyEffect  ——  常驻 Buff 应用
    // ----------------------------------------------------------------------------
    //  在 UpdateAccessory 中每帧重新 AddBuff(id, 2) 即可保证 buff 永远不消失
    //  (vanilla AddBuff 用于刷新计时, 重复添加无副作用)
    //
    //  使用示例:
    //
    //    BuffApplyEffect.Apply(player,
    //        BuffID.Honey,
    //        BuffID.WellFed3,
    //        BuffID.DryadsWard,
    //        BuffID.NebulaUpMana3,
    //        ModContent.BuffType<GravityNormalizerBuff>()
    //    );
    //
    //  duration 默认 2 tick (足够维持到下帧再次刷新)。
    //  如果想用更长的, 传入 duration 参数。
    // ============================================================================

    public static class BuffApplyEffect
    {
        public static void Apply(Player player, params int[] buffIds)
        {
            for (int i = 0; i < buffIds.Length; i++)
            {
                int id = buffIds[i];
                if (id > 0) player.AddBuff(id, 2);
            }
        }

        public static void Apply(Player player, int duration, params int[] buffIds)
        {
            for (int i = 0; i < buffIds.Length; i++)
            {
                int id = buffIds[i];
                if (id > 0) player.AddBuff(id, duration);
            }
        }
    }
}
