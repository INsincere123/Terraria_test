using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Items.Armor.JuLing
{
    [AutoloadEquip(EquipType.Legs)]
    public class JuLingLeggings : ModItem
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const int   Defense        = 13;
        public const float MagicDamage    = 0.05f;  // +5% 魔法伤害
        public const float MoveSpeedBoost = 0.05f;  // +5% 移动速度
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.width   = 22;
            Item.height  = 18;
            Item.defense = Defense;
            Item.value   = Item.sellPrice(gold: 8);
            Item.rare    = ItemRarityID.Yellow;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Magic) += MagicDamage;
            player.moveSpeed                    += MoveSpeedBoost;
        }
    }
}
