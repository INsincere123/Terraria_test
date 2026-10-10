using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.DataStructures;
using TestMod.Common.Mechanics.AccessoryEffects;

namespace TestMod.Content.Items.Accessories
{
    /// <summary>
    /// 超能护盾 — 能量护盾的升级版。
    ///
    /// 护盾数值：60 + 120% 防御力 + 9% 最大生命值
    /// 护盾效果：+5% 减伤、+4% 伤害、+2% 暴击率、免疫击退（来自 EnergyShieldPlayer 全局）
    /// 恢复规则：不衰减；11 秒未受击后，每秒恢复普通总上限的 1/6
    /// 紧急护盾：生命值跌破 30% 时触发，护盾值 = 50%最大生命 + 100%防御，
    ///           4.5 秒内线性衰减，冷却 120 秒（Buff 图标显示倒计时）
    ///
    /// ⚠️ 占位贴图：当前指向原版圣骑士护盾图标（ItemID 938）。
    ///    替换为 Content/Items/Accessories/SuperEnergyShieldItem.png 后删除 Texture override。
    /// </summary>
    public class SuperEnergyShieldItem : ModItem
    {
        // 开发期占位贴图；替换为自定义 PNG 后删除此行
        public override string Texture => "Terraria/Images/Item_938";

        // ── 数值调节区 ────────────────────────────────────────────────
        public const float ShieldBase       = 60f;   // 固定护盾基础值
        public const float ShieldDefRatio   = 1.20f; // 防御力转化系数（+120%防御）
        public const float ShieldLifeRatio  = 0.09f; // 最大生命值转化系数
        public const float EnduranceBonus   = 0.05f; // 护盾存活时减伤加成（5%）
        public const float DamageBonus      = 0.04f; // 护盾存活时伤害加成（4%）
        public const int   CritBonus        = 2;     // 护盾存活时暴击加成（2%）
        // ─────────────────────────────────────────────────────────────

        // 护盾颜色（比能量护盾偏蓝紫，有升级感）
        private static readonly Color MainColor = new Color(100, 140, 255);
        private static readonly Color EdgeColor = new Color(200, 215, 255);

        private static readonly ShieldDefinition Definition = new()
        {
            GetMaxShield = static p => ShieldBase + p.statDefense * ShieldDefRatio + p.statLifeMax2 * ShieldLifeRatio,
            OnActive = static p =>
            {
                p.endurance += EnduranceBonus;
                p.GetDamage(DamageClass.Generic) += DamageBonus;
                p.GetCritChance(DamageClass.Generic) += CritBonus;
            },
            ShieldColor = MainColor,
            ShieldEdgeColor = EdgeColor,
        };

        public override void SetDefaults()
        {
            Item.width     = 34;
            Item.height    = 30;
            Item.defense   = 7;
            Item.value     = Item.buyPrice(gold: 20);
            Item.rare      = ItemRarityID.LightRed;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
            => ApplyEffects(player);

        internal static void ApplyEffects(Player player)
        {
            player.GetModPlayer<SuperEnergyShieldPlayer>().IsEquipped = true;
            ShieldEffect.Apply(player, Definition);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
            => AddShieldTooltip(tooltips, Mod);

        internal static void AddShieldTooltip(List<TooltipLine> tooltips, Mod mod)
        {
            Player player    = Main.LocalPlayer;
            var    sp        = player.GetModPlayer<EnergyShieldPlayer>();
            int    maxShield = (int)(ShieldBase + player.statDefense * ShieldDefRatio + player.statLifeMax2 * ShieldLifeRatio);

            int idx = tooltips.FindIndex(t => t.Name == "Tooltip0");
            if (idx >= 0)
            {
                tooltips.Insert(idx + 1, new TooltipLine(mod, "ShieldCurrent",
                    sp.MaxShield > 0f ? $"当前护盾值 [c/64C8FF:{System.Math.Ceiling(sp.CurrentShield):0}] / [c/64C8FF:{System.Math.Ceiling(sp.MaxShield):0}]" : $"预计普通护盾上限 [c/64C8FF:{maxShield}]"));
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<EnergyShieldItem>()
                .AddIngredient(ItemID.PaladinsShield)  // 圣骑士护盾（ItemID 938）
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
