using TestMod.Content.Rarities;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Armor.UltimateArmor
{
    [LegacyName("AntaresBreastplate")]
    [AutoloadEquip(EquipType.Body)]
    public class UltimateBreastplate : ToggleableArmorItem
    {
        protected override string ToggleEffectNameKey => "Mods.TestMod.Items.UltimateBreastplate.ToggleEffectName";

        // ╔══════════════════════════════════════════════════════╗
        // ║              胸甲数值调整区域                        ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【基础防御】                                        ║
        // ║    Defense              物品本身的防御力             ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【攻击属性】  适用于所有职业                        ║
        // ║    DamageBonus          +X% 伤害，1.0f = +100%      ║
        // ║    CritBonus            +X% 暴击率                  ║
        // ║    AttackSpeedBonus     +X% 攻速，0.35f = +35%      ║
        // ║    ArmorPenetration     +X 穿甲值                   ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【防御属性】                                        ║
        // ║    MaxLifeBonus         +X% 最大生命值上限           ║
        // ║    MaxManaBonus         +X% 最大法力值上限           ║
        // ║    DamageReductionBonus +X% 免伤（乘法叠加）         ║
        // ║    DefenseBonus         +X 防御值（加法叠加）        ║
        // ║    LifeRegenBonus       +X 生命再生（2 = +1 HP/s）   ║
        // ║    ManaRegenBonus       +X 法力恢复倍率              ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【召唤属性】                                        ║
        // ║    ExtraMinionSlots     +X 召唤物上限槽位            ║
        // ║    ExtraSentrySlots     +X 哨兵上限槽位              ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【移动属性】                                        ║
        // ║    MoveSpeedBonus       +X% 移动速度                 ║
        // ╚══════════════════════════════════════════════════════╝

        public const int   Defense              = 100;

        public const float DamageBonus          = 0.33f;
        public const int   CritBonus            = 12;
        public const float AttackSpeedBonus     = 0.12f;
        public const int   ArmorPenetration     = 8;

        public const float MaxLifeBonus         = 0.33f;
        public const float MaxManaBonus         = 0.33f;
        public const float DamageReductionBonus = 0.05f;
        public const int   DefenseBonus         = 5;
        public const int   LifeRegenBonus       = 10;
        public const float ManaRegenBonus       = 1f;

        public const int   ExtraMinionSlots     = 3;
        public const int   ExtraSentrySlots     = 1;

        //public const float MoveSpeedBonus       = 0.03f;

        // ══════════════════════════════════════════════════════

        public override void SetDefaults()
        {
            Item.width   = 26;
            Item.height  = 22;
            Item.value   = 1000000;
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.defense = Defense;
        }

        public override void UpdateEquip(Player player)
        {
            // 攻击属性
            player.GetDamage(DamageClass.Generic)           += DamageBonus;
            player.GetCritChance(DamageClass.Generic)       += CritBonus;
            player.GetAttackSpeed(DamageClass.Generic)      += AttackSpeedBonus;
            player.GetArmorPenetration(DamageClass.Generic) += ArmorPenetration;

            // 防御属性
            player.DefenseEffectiveness *= (1f + DamageReductionBonus);
            player.statDefense          += DefenseBonus;
            player.lifeRegen            += LifeRegenBonus;
            player.manaRegenBonus       += (int)ManaRegenBonus;

            // 召唤属性
            player.maxMinions += ExtraMinionSlots;
            player.maxTurrets += ExtraSentrySlots;

            // 移动属性
            //player.moveSpeed  += MoveSpeedBonus;

            // 标记供 AntaresArmorPlayer 处理百分比生命/法力加成
            AntaresArmorPlayer armorPlayer = player.GetModPlayer<AntaresArmorPlayer>();
            armorPlayer.wearingBreastplate = true;
            armorPlayer.phantasmalDragonEnabled = EffectEnabled;
        }

        public override void AddRecipes()
            => global::TestMod.Common.Utilities.MasterTrophyRecipes.Register(Type);
    }
}
