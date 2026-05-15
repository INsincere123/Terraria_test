using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Items.Armor.JuLing
{
    [AutoloadEquip(EquipType.Body)]
    public class JuLingRobe : ModItem
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const int   Defense     = 16;
        public const float MagicDamage = 0.07f;  // +7% 魔法伤害
        public const int   MagicCrit   = 7;      // +7% 魔法暴击
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.width   = 22;
            Item.height  = 20;
            Item.defense = Defense;
            Item.value   = Item.sellPrice(gold: 8);
            Item.rare    = ItemRarityID.Yellow;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Magic)    += MagicDamage;
            player.GetCritChance(DamageClass.Magic) += MagicCrit;
        }
    }
}
