using Terraria;
using Terraria.Localization;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Armor.JuLing
{
    [AutoloadEquip(EquipType.Head)]
    public class JuLingHat : ModItem
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const int   Defense        = 12;
        public const float MagicDamage    = 0.10f;  // +10% 魔法伤害
        public const int   MagicCrit      = 10;     // +10% 魔法暴击
        public const int   MaxManaBoost   = 80;     // +80 最大法力
        public const float ManaCostReduce = 0.10f;  // -10% 魔力消耗

        // 套装加成（头盔持有，供 hjson tooltip 引用）
        public const float SetMagicDamage = 0.08f;
        public const int   SetMagicCrit   = 8;
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.width   = 22;
            Item.height  = 20;
            Item.defense = Defense;
            Item.value   = Item.sellPrice(gold: 8);
            Item.rare    = ItemRarityID.Yellow;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
            => body.type == ModContent.ItemType<JuLingRobe>()
            && legs.type == ModContent.ItemType<JuLingLeggings>();

        public override void UpdateArmorSet(Player player)
        {
            // 套装加成：魔法属性
            player.GetDamage(DamageClass.Magic)    += SetMagicDamage;
            player.GetCritChance(DamageClass.Magic) += SetMagicCrit;

            // 标记 ModPlayer，供按键系统判断是否可激活
            player.GetModPlayer<JuLingPlayer>().HasJuLingSet = true;

            // setBonus 文字（从本地化读取，含按键占位符）
            string key = JuLingKeybind.AbilityKey?.GetAssignedKeys().Count > 0
                ? JuLingKeybind.AbilityKey.GetAssignedKeys()[0]
                : Language.GetTextValue("Mods.TestMod.Common.Unbound");
            player.setBonus = Language.GetTextValue("Mods.TestMod.Items.JuLingHat.SetBonus", (int)(SetMagicDamage * 100), SetMagicCrit, key);
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Magic)    += MagicDamage;
            player.GetCritChance(DamageClass.Magic) += MagicCrit;
            player.statManaMax2                     += MaxManaBoost;
            player.manaCost                         -= ManaCostReduce;
        }
    }
}
