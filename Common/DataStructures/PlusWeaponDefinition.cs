using Terraria.ID;

namespace TestMod.Common.DataStructures
{
    public enum PlusWeaponKind : byte
    {
        None, Phantasm, Daybreak, NebulaBlaze, StardustDragon, StardustCell,
        LunarPortal, RainbowCrystal, Terraprisma, Kaleidoscope
    }

    public readonly record struct PlusWeaponDefinition(int OriginalType, float WeaponDamage,
        float Knockback = 1f)
    {
        public static PlusWeaponDefinition For(PlusWeaponKind kind) => kind switch
        {
            PlusWeaponKind.Phantasm => new(ItemID.Phantasm, 3.6f),
            PlusWeaponKind.Daybreak => new(ItemID.DayBreak, 14f, 2f),
            PlusWeaponKind.NebulaBlaze => new(ItemID.NebulaBlaze, 1.8f),
            PlusWeaponKind.StardustDragon => new(ItemID.StardustDragonStaff, 11f),
            PlusWeaponKind.StardustCell => new(ItemID.StardustCellStaff, 18f, 2f),
            PlusWeaponKind.LunarPortal => new(ItemID.MoonlordTurretStaff, 15f, 5f),
            PlusWeaponKind.RainbowCrystal => new(ItemID.RainbowCrystalStaff, 15f, 2f),
            PlusWeaponKind.Terraprisma => new(ItemID.EmpressBlade, 15f, 2f),
            PlusWeaponKind.Kaleidoscope => new(ItemID.RainbowWhip, 6.3f),
            _ => new(0, 1f)
        };

        public static bool MatchesProjectile(PlusWeaponKind kind, int type) => kind switch
        {
            PlusWeaponKind.Phantasm => type == ProjectileID.PhantasmArrow,
            PlusWeaponKind.Daybreak => type is ProjectileID.Daybreak or ProjectileID.DaybreakExplosion,
            PlusWeaponKind.NebulaBlaze => type is ProjectileID.NebulaBlaze1 or ProjectileID.NebulaBlaze2,
            PlusWeaponKind.StardustDragon => ProjectileID.Sets.StardustDragon[type],
            PlusWeaponKind.StardustCell => type is ProjectileID.StardustCellMinion or ProjectileID.StardustCellMinionShot,
            PlusWeaponKind.LunarPortal => type is ProjectileID.MoonlordTurret or ProjectileID.MoonlordTurretLaser,
            PlusWeaponKind.RainbowCrystal => type is ProjectileID.RainbowCrystal or ProjectileID.RainbowCrystalExplosion,
            PlusWeaponKind.Terraprisma => type == ProjectileID.EmpressBlade,
            PlusWeaponKind.Kaleidoscope => type == ProjectileID.RainbowWhip,
            _ => false
        };
    }
}
