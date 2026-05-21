using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.DataStructures;
using TestMod.Items.Accessories.Effects;

namespace TestMod.Items.Accessories
{
    /// <summary>
    /// 超能护盾 — 能量护盾的升级版。
    ///
    /// 护盾数值：60 + 120% 防御力
    /// 护盾效果：+5% 减伤、+4% 伤害、+2% 暴击率、免疫击退（来自 EnergyShieldPlayer 全局）
    /// 恢复规则：不衰减；受击后 5 秒未再受击、2 秒逐渐恢复满
    /// 紧急护盾：生命值跌破 30% 时触发，护盾值 = 50%最大生命 + 100%防御，
    ///           4.5 秒内线性衰减，冷却 120 秒（Buff 图标显示倒计时）
    ///
    /// ⚠️ 占位贴图：当前指向原版圣骑士护盾图标（ItemID 938）。
    ///    替换为 Items/Accessories/SuperEnergyShieldItem.png 后删除 Texture override。
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
        public const int   RechargeDelaySec = 5;     // 受击后延迟恢复（秒）
        // ─────────────────────────────────────────────────────────────

        // 护盾颜色（比能量护盾偏蓝紫，有升级感）
        private static readonly Color MainColor = new Color(100, 140, 255);
        private static readonly Color EdgeColor = new Color(200, 215, 255);

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
        {
            // 标记装备状态（供紧急护盾触发检测使用）
            var ssp = player.GetModPlayer<SuperEnergyShieldPlayer>();
            ssp.IsEquipped = true;

            ShieldEffect.Apply(player, new ShieldDefinition
            {
                // 护盾上限 = 固定 60 + 防御力的 120%
                // 若紧急护盾激活，额外叠加随时间衰减的紧急护盾值
                GetMaxShield = p =>
                {
                    float normal = ShieldBase + p.statDefense * ShieldDefRatio + p.statLifeMax2 * ShieldLifeRatio;
                    if (!ssp.EmergencyActive) return normal;
                    float frac = (float)ssp.EmergencyTimer / SuperEnergyShieldPlayer.EmergencyFrames;
                    return normal + ssp.EmergencyMaxValue * frac;
                },

                // 不自动衰减（紧急护盾的衰减通过 GetMaxShield 动态实现）
                DecayPerSecond = 0f,

                // 5 秒延迟后 2 秒恢复满（速率 = 最大护盾 ÷ 2 秒）
                RechargeDelayFrames  = RechargeDelaySec * 60,
                GetRechargePerSecond = p => (ShieldBase + p.statDefense * ShieldDefRatio) / 2f,

                // 护盾存活时：减伤 / 伤害 / 暴击
                OnActive = p =>
                {
                    p.endurance                             += EnduranceBonus;
                    p.GetDamage(DamageClass.Generic)        += DamageBonus;
                    p.GetCritChance(DamageClass.Generic)    += CritBonus;
                },

                OnBreak = null,

                ShieldColor     = MainColor,
                ShieldEdgeColor = EdgeColor,
            });
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Player player    = Main.LocalPlayer;
            var    ssp       = player.GetModPlayer<SuperEnergyShieldPlayer>();
            var    sp        = player.GetModPlayer<EnergyShieldPlayer>();
            int    maxShield = (int)(ShieldBase + player.statDefense * ShieldDefRatio + player.statLifeMax2 * ShieldLifeRatio);

            int idx = tooltips.FindIndex(t => t.Name == "Tooltip0");
            if (idx >= 0)
            {
                tooltips.Insert(idx + 1, new TooltipLine(Mod, "ShieldCurrent",
                    $"当前护盾值 [c/64C8FF:{(int)sp.CurrentShield}] / [c/64C8FF:{maxShield}]"));
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
