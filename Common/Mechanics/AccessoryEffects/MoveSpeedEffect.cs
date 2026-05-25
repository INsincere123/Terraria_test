using Terraria;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    // ============================================================================
    //  MoveSpeedEffect  ——  地面移动综合 (移速 / 鞋子 / 击退免疫 / 长无敌帧)
    // ----------------------------------------------------------------------------
    //  使用示例:
    //
    //    MoveSpeedEffect.Apply(player, new MoveSpeedConfig {
    //        MoveSpeed     = 0.10f,   // +10% 移速
    //        RunSpeedCap   = 18f,     // 奔跑速度上限 (vanilla 默认 6, 火神靴 9)
    //        IceSkate      = true,
    //        WaterWalk     = true,
    //        FireBlockImmune = true,
    //        LavaImmune    = true,
    //        LavaImmuneTimeBonus = 420, // 增加熔岩免疫上限 (单位 tick)
    //        NoKnockback   = true,
    //        LongInvince   = true,
    //    });
    // ============================================================================

    public struct MoveSpeedConfig
    {
        public float MoveSpeed;            // 0.10f = +10% 移速
        public float RunSpeedCap;          // 奔跑速度上限 (设为 0 不修改)
        public bool  IceSkate;             // 冰鞋 (冰面不打滑)
        public bool  WaterWalk;            // 水上行走
        public bool  FireBlockImmune;      // 免疫火砖伤害
        public bool  LavaImmune;           // 免疫熔岩伤害
        public int   LavaImmuneTimeBonus;  // 增加熔岩免疫上限 (tick)
        public bool  NoKnockback;          // 免疫击退
        public bool  LongInvince;          // 延长受伤无敌帧时间
    }

    public static class MoveSpeedEffect
    {
        public static void Apply(Player player, MoveSpeedConfig cfg)
        {
            if (cfg.MoveSpeed != 0f)
                player.moveSpeed += cfg.MoveSpeed;

            if (cfg.RunSpeedCap > 0f && player.accRunSpeed < cfg.RunSpeedCap)
                player.accRunSpeed = cfg.RunSpeedCap;

            if (cfg.IceSkate)        player.iceSkate    = true;
            if (cfg.WaterWalk)       player.waterWalk   = true;
            if (cfg.FireBlockImmune) player.fireWalk    = true;
            if (cfg.LavaImmune)      player.lavaImmune  = true;
            if (cfg.LavaImmuneTimeBonus > 0) player.lavaMax += cfg.LavaImmuneTimeBonus;
            if (cfg.NoKnockback)     player.noKnockback = true;
            if (cfg.LongInvince)     player.longInvince = true;
        }
    }
}
