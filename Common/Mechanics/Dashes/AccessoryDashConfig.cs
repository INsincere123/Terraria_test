namespace TestMod.Common.Mechanics.Dashes
{
    // 值的顺序同时代表这三件来源之间的选择优先级，不由功能槽顺序决定。
    public enum AccessoryDashSource { Cthulhu, Veil, TerraWings }
    public enum AccessoryDashMotion { Segmented, FixedDuration }
    public enum AccessoryDashVisual { Shadow, Smoke, Terra }

    /// <summary>
    /// 共享定义只保存不可变参数；每次启动将选中的定义固定到玩家状态。
    /// 速度单位为像素/tick，时间单位为游戏tick。分段模式保留贴墙起步减半与跑速收尾。
    /// 固定时长模式的 ExitRetention 只限制同向结束残速上限，不抬高已有低速；松键/反向归零。
    /// ContactImmunity 只保护NPC接触，不写通用无敌帧。
    /// </summary>
    public sealed record AccessoryDashConfig(
        AccessoryDashSource Source, AccessoryDashMotion Motion,
        float StartSpeed, float FastRetention, float SlowRetention,
        float FastThreshold, int Duration, int Cooldown, float ExitRetention,
        int Damage, float Knockback, int HitboxExpand, int ContactImmunity,
        AccessoryDashVisual Visual);
}
