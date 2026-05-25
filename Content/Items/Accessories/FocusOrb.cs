using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Common.Mechanics.AccessoryEffects;

namespace TestMod.Content.Items.Accessories
{
    public class FocusOrb : ModItem
    {
        // ── 被动属性调节区 ────────────────────────────────────────────
        private const float DamageBonus       = 0.08f; // +8% 全职业伤害
        private const int   CritBonus         = 6;     // +6% 暴击率
        private const int   ArmorPeneBonus    = 4;     // +4 护甲穿透
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.width     = 32;
            Item.height    = 32;
            Item.accessory = true;
            Item.value     = Item.sellPrice(gold: 15);
            Item.rare      = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<FocusOrbPlayer>().HasFocusOrb = true;

            CombatStatsEffect.Apply(player, new CombatStatsConfig
            {
                Damage           = DamageBonus,
                Crit             = CritBonus,
                ArmorPenetration = ArmorPeneBonus,
                ClassType        = DamageClass.Generic,
            });
        }

        public override void ModifyTooltips(System.Collections.Generic.List<Terraria.ModLoader.TooltipLine> tooltips)
        {
            string keyStr = FocusOrbKeybind.AbilityKey?.GetAssignedKeys().Count > 0
                ? FocusOrbKeybind.AbilityKey.GetAssignedKeys()[0]
                : "未绑定";

            var line = new TooltipLine(Mod, "FocusOrbAbility",
                $"按 [{keyStr}] 激活：接下来 7 次命中必定暴击且弹幕自带追踪\n" +
                $"  本该暴击时，额外造成[c/FF4500:200% ]的真实伤害\n" +
                $"  [c/AAAAAA:冷却30秒]");

            int idx = tooltips.FindIndex(t => t.Name == "Tooltip0");
            if (idx >= 0) tooltips.Insert(idx + 1, line);
            else          tooltips.Add(line);
        }
    }
}
