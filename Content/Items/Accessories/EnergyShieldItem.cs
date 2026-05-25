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
    /// 能量护盾 — 护盾系统的第一个实装饰品。
    ///
    /// 护盾数值：固定 20 + 10% 最大生命值
    /// 护盾效果：护盾存活时 +5 防御力
    /// 恢复规则：不自动衰减；受击后 6 秒未再受击瞬间恢复满
    ///
    /// ⚠️ 占位贴图：当前指向原版 Shackle 物品。
    ///    替换为 Content/Items/Accessories/EnergyShieldItem.png 后删除 Texture override。
    /// </summary>
    public class EnergyShieldItem : ModItem
    {
        // 开发期占位贴图；替换为自定义 PNG 后删除此行
        public override string Texture => "Terraria/Images/Item_" + ItemID.Shackle;

        // ── 数值调节区 ────────────────────────────────────────────────
        public const float ShieldBase       = 20f;   // 固定护盾基础值
        public const float ShieldLifeRatio  = 0.06f; // 最大生命值转化系数
        public const int   DefenseBonus     = 5;     // 护盾存活时的防御加成
        public const int   RechargeDelaySec = 6;     // 受击后多少秒未再受击才开始恢复
        // ─────────────────────────────────────────────────────────────

        // 护盾颜色（蓝色系能量感）
        private static readonly Color MainColor = new Color(80, 160, 255);
        private static readonly Color EdgeColor = new Color(160, 220, 255);

        public override void SetDefaults()
        {
            Item.width     = 24;
            Item.height    = 24;
            Item.defense   = 1;
            Item.value     = Item.buyPrice(gold: 5);
            Item.rare      = ItemRarityID.Blue;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            ShieldEffect.Apply(player, new ShieldDefinition
            {
                // 护盾上限 = 固定 20 + 最大生命值的 10%
                GetMaxShield        = p => ShieldBase + p.statLifeMax2 * ShieldLifeRatio,

                // 不自动衰减，仅受击消耗
                DecayPerSecond      = 0f,

                // 6 秒未受击后瞬间恢复满
                RechargeDelayFrames = RechargeDelaySec * 60,
                RechargePerSecond   = float.MaxValue,

                // 护盾存活时：+5 防御力
                OnActive            = p => p.statDefense += DefenseBonus,

                // 破盾时无特效（可在此加粒子/音效）
                OnBreak             = null,

                ShieldColor         = MainColor,
                ShieldEdgeColor     = EdgeColor,
            });
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // 计算当前护盾上限供显示（用本地玩家属性实时计算）
            Player   player    = Main.LocalPlayer;
            int      maxShield = (int)(ShieldBase + player.statLifeMax2 * ShieldLifeRatio);
            float    current   = player.GetModPlayer<EnergyShieldPlayer>().CurrentShield;

            // 在第一行描述后追加动态护盾量信息
            int idx = tooltips.FindIndex(t => t.Name == "Tooltip0");
            if (idx >= 0)
            {
                tooltips.Insert(idx + 1, new TooltipLine(Mod, "ShieldCurrent",
                    $"当前护盾值 [c/50A0FF:{(int)current}] / [c/50A0FF:{maxShield}]"));
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.IronBar, 10)  // 10 个铁锭
                .AddIngredient(ItemID.FallenStar, 3)
                .AddTile(TileID.Anvils)
                .Register();
            CreateRecipe()
                .AddIngredient(ItemID.LeadBar, 10)  // 10 个铅锭
                .AddIngredient(ItemID.FallenStar, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
