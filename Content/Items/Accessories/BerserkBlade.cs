using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Content.Items.Accessories.Effects;

namespace TestMod.Content.Items.Accessories
{
    public class BerserkBlade : ModItem
    {
        public const float DamageBonus = 0.04f;
        public const int CritBonus = 2;
        public const float AttackSpeedBonus = 0.05f;
        public const int ArmorPenetration = 2;
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.accessory = true;
            Item.value = Item.sellPrice(gold: 15);
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {

            CombatStatsEffect.Apply(player, new CombatStatsConfig
            {
                Damage = DamageBonus,
                Crit = CritBonus,
                AttackSpeed = AttackSpeedBonus,
                ArmorPenetration = ArmorPenetration,
                ClassType = DamageClass.Melee,
            });
            player.GetModPlayer<BerserkBladePlayer>().HasBerserkBlade = true;
        }
    }
}
