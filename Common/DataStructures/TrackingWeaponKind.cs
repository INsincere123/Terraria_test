namespace TestMod.Common.DataStructures
{
    // 与已有 PlusWeaponKind 分开，避免继承旧强化武器的伤害、配方或兼容规则。
    public enum TrackingWeaponKind : byte
    {
        None, DesertTiger, Raven, Abigail, Spider
    }
}
