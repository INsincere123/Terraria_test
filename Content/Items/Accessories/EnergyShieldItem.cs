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
    /// 护盾数值：固定 20 + 6% 最大生命值
    /// 护盾效果：护盾存活时 +5 防御力
    /// 恢复规则：不自动衰减；11 秒未受击后，每秒恢复普通总上限的 1/6
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
        // ─────────────────────────────────────────────────────────────

        // 护盾颜色（蓝色系能量感）
        private static readonly Color MainColor = new Color(80, 160, 255);
        private static readonly Color EdgeColor = new Color(160, 220, 255);

        // 复用静态定义及静态委托，不在每帧分配配置或捕获玩家对象。
        private static readonly ShieldDefinition Definition = new()
        {
            GetMaxShield = static p => ShieldBase + p.statLifeMax2 * ShieldLifeRatio,
            OnActive = static p => p.statDefense += DefenseBonus,
            ShieldColor = MainColor,
            ShieldEdgeColor = EdgeColor,
        };

        public override void SetDefaults()
        {
            Item.width     = 24;
            Item.height    = 24;
            Item.defense   = 1;
            Item.value     = Item.buyPrice(gold: 5);
            Item.rare      = ItemRarityID.Blue;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) => ShieldEffect.Apply(player, Definition);

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // 计算当前护盾上限供显示（用本地玩家属性实时计算）
            Player   player    = Main.LocalPlayer;
            int      maxShield = (int)(ShieldBase + player.statLifeMax2 * ShieldLifeRatio);
            var shield = player.GetModPlayer<EnergyShieldPlayer>();

            // 在第一行描述后追加动态护盾量信息
            int idx = tooltips.FindIndex(t => t.Name == "Tooltip0");
            if (idx >= 0)
            {
                tooltips.Insert(idx + 1, new TooltipLine(Mod, "ShieldCurrent",
                    shield.MaxShield > 0f ? $"当前护盾值 [c/50A0FF:{System.Math.Ceiling(shield.CurrentShield):0}] / [c/50A0FF:{System.Math.Ceiling(shield.MaxShield):0}]" : $"预计普通护盾上限 [c/50A0FF:{maxShield}]"));
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
