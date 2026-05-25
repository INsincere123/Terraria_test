using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Content.Items.Accessories.Effects;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Accessories
{
    /// <summary>
    /// 收集者 — 处决 Boss、积累战利品的猎人饰品。
    ///
    /// 起始无属性；处决 Boss 后按阶段增强。
    /// 处决：Boss 血量低于 7% 时，任意命中触发必杀
    /// 奖励（Boss 完全死亡后）：铂金奖励 + Lucky Buff 20 分钟
    ///
    /// ⚠️ 占位贴图：当前指向原版钻石物品。
    ///    替换为 Content/Items/Accessories/CollectorItem.png 后删除 Texture override。
    /// </summary>
    public class CollectorItem : ModItem
    {
        // 开发期占位贴图；替换为自定义 PNG 后删除此行
        //public override string Texture => "Terraria/Images/Item_" + ItemID.Diamond;

        // ── 数值调节区 ────────────────────────────────────────────────
        public const int   EarlyUnlockExecutions = 2;
        public const int   EarlyArmorPenetration = 5;
        public const int   HardmodeArmorPenetration = 10;
        public const float HardmodeDamage = 0.05f;
        public const int   PlanteraCrit = 5;
        public const int   GolemCrit = 10;
        public const int   MoonLordArmorPenetration = 20;
        public const float MoonLordDamage = 0.20f;
        public const int   MoonLordCrit = 15;
        public const int   ElectrifiedDebuffDuration = 300;
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.width     = 24;
            Item.height    = 24;
            Item.value     = Item.buyPrice(gold: 30);
            Item.rare      = Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 标记装备状态（处决与奖励逻辑在 CollectorPlayer / CollectorSystem 中执行）
            CollectorPlayer collector = player.GetModPlayer<CollectorPlayer>();
            collector.IsEquipped = true;

            CombatStatsConfig stats = GetCurrentStats(collector);
            CombatStatsEffect.Apply(player, stats);

            if (collector.ExecutedMoonLord)
            {
                player.GetModPlayer<CorePlayer>().independentDamageMult *= 1.05f;
                OnHitDebuffPlayer.AddDebuff(player, BuffID.Electrified, ElectrifiedDebuffDuration);
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            CollectorPlayer collector = Main.LocalPlayer.GetModPlayer<CollectorPlayer>();
            CombatStatsConfig stats = GetCurrentStats(collector);

            string statsLine = BuildStatsLine(stats, collector.ExecutedMoonLord);
            int index = tooltips.FindIndex(t => t.Name == "Tooltip0");
            if (index >= 0)
                tooltips.Insert(index + 1, new TooltipLine(Mod, "CollectorStats", statsLine));
            else
                tooltips.Add(new TooltipLine(Mod, "CollectorStats", statsLine));
        }

        private static CombatStatsConfig GetCurrentStats(CollectorPlayer collector)
        {
            if (collector.ExecutedMoonLord)
            {
                return new CombatStatsConfig
                {
                    Damage = MoonLordDamage,
                    Crit = MoonLordCrit,
                    ArmorPenetration = MoonLordArmorPenetration,
                    ClassType = DamageClass.Generic,
                };
            }

            int armorPenetration = 0;
            float damage = 0f;
            int crit = 0;

            if (collector.ExecutedBossCount >= EarlyUnlockExecutions)
            {
                armorPenetration = EarlyArmorPenetration;

                if (Main.hardMode)
                {
                    armorPenetration = HardmodeArmorPenetration;
                    damage = HardmodeDamage;
                }
            }

            if (collector.ExecutedGolem)
                crit = GolemCrit;
            else if (collector.ExecutedPlantera)
                crit = PlanteraCrit;

            return new CombatStatsConfig
            {
                Damage = damage,
                Crit = crit,
                ArmorPenetration = armorPenetration,
                ClassType = DamageClass.Generic,
            };
        }

        private static string BuildStatsLine(CombatStatsConfig stats, bool electrified)
        {
            if (stats.ArmorPenetration == 0 && stats.Damage == 0f && stats.Crit == 0 && !electrified)
                return "[c/AAAAAA:当前阶段：无属性加成]";

            List<string> parts = new();
            if (stats.ArmorPenetration != 0)
                parts.Add($"[c/FFD700:+{stats.ArmorPenetration} 护甲穿透]");
            if (stats.Damage != 0f)
                parts.Add($"[c/FFD700:+{(int)(stats.Damage * 100f)}% 伤害]");
            if (stats.Crit != 0)
                parts.Add($"[c/FFD700:+{stats.Crit}% 暴击率]");
            if (electrified)
            {
                parts.Add("[c/FFD700:伤害变为 1.05 倍]");
                parts.Add("[c/66CCFF:命中附着带电]");
            }

            return string.Join("，", parts);
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.GoldBar, 30)  // 30 个金锭
                .AddTile(TileID.Anvils)
                .Register();
            CreateRecipe()
                .AddIngredient(ItemID.PlatinumBar, 30)
                .AddTile(TileID.Anvils)
                .Register();
        }
        
    }
}
