using Microsoft.Xna.Framework;

namespace TestMod.Common.Compatibility
{
    // 样式键属于跨模组接口；这里只保留缺失增强模组时所需的颜色。
    public static class TestModTextStyles
    {
        public const string DamageMelee = "Damage.Melee";
        public const string DamageRanged = "Damage.Ranged";
        public const string DamageMagic = "Damage.Magic";
        public const string DamageSummon = "Damage.Summon";
        public const string DamageTrue = "Damage.True";
        public const string ExtraHit = "Combat.ExtraHit";
        public const string BlackHoleAbsorb = "Combat.BlackHoleAbsorb";
        public const string Subhand = "Combat.Subhand";
        public const string Supercrit = "Combat.Supercrit";
        public const string BloodFeedBerserk = "Combat.BloodFeedBerserk";
        public const string Heartsteel = "Combat.Heartsteel";
        public const string EnergyShieldDamage = "EnergyShield.Damage";
        public const string EnergyShieldBreak = "EnergyShield.Break";
        public const string RarityAntares = "Rarity.Antares";
        public const string RarityEventHorizon = "Rarity.EventHorizon";
        public const string RarityTerra = "Rarity.Terra";

        public static Color GetFallbackColor(string key) => key switch
        {
            DamageMelee => new Color(255, 24, 34),
            DamageRanged => new Color(58, 238, 210),
            DamageMagic => new Color(124, 74, 255),
            DamageSummon => new Color(82, 232, 154),
            DamageTrue => Color.White,
            ExtraHit => new Color(180, 50, 255),
            BlackHoleAbsorb => new Color(218, 148, 40),
            Subhand => new Color(150, 255, 188),
            Supercrit => new Color(255, 214, 62),
            BloodFeedBerserk => new Color(220, 18, 48),
            Heartsteel => new Color(190, 70, 255),
            EnergyShieldDamage or EnergyShieldBreak => new Color(64, 224, 255),
            RarityAntares => new Color(30, 60, 180),
            RarityEventHorizon => new Color(168, 104, 196),
            RarityTerra => new Color(36, 184, 106),
            _ => new Color(180, 50, 255)
        };
    }
}
