using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Items.Accessories
{
    /// <summary>
    /// 收集者 — 处决 Boss、积累战利品的猎人饰品。
    ///
    /// 被动（困难模式前）：+5 护甲穿透
    /// 被动（困难模式）：+10 护甲穿透、+8% 暴击率
    /// 处决：Boss 血量低于 5% 时，任意命中触发必杀
    /// 奖励（Boss 完全死亡后）：铂金奖励 + Lucky Buff 20 分钟
    ///
    /// ⚠️ 占位贴图：当前指向原版钻石物品。
    ///    替换为 Items/Accessories/CollectorItem.png 后删除 Texture override。
    /// </summary>
    public class CollectorItem : ModItem
    {
        // 开发期占位贴图；替换为自定义 PNG 后删除此行
        //public override string Texture => "Terraria/Images/Item_" + ItemID.Diamond;

        // ── 数值调节区 ────────────────────────────────────────────────
        public const int PreHardmodeAP  = 5;  // 困难模式前护甲穿透
        public const int HardmodeAP     = 10; // 困难模式护甲穿透
        public const int HardmodeCrit   = 7;  // 困难模式暴击率加成（%）
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.width     = 24;
            Item.height    = 24;
            Item.value     = Item.buyPrice(gold: 30);
            Item.rare      = ItemRarityID.Yellow;
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            if (Main.hardMode)
            {
                // 击败血肉墙后解锁额外加成
                player.GetArmorPenetration(DamageClass.Generic) += HardmodeAP;
                player.GetCritChance(DamageClass.Generic)       += HardmodeCrit;
            }
            else
            {
                player.GetArmorPenetration(DamageClass.Generic) += PreHardmodeAP;
            }

            // 标记装备状态（处决与奖励逻辑在 CollectorPlayer / CollectorSystem 中执行）
            player.GetModPlayer<CollectorPlayer>().IsEquipped = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // 根据当前模式动态显示被动属性行
            string statsLine = Main.hardMode
                ? $"[c/FFD700:+{HardmodeAP} 护甲穿透]，[c/FFD700:+{HardmodeCrit}% 暴击率]"
                : $"[c/FFD700:+{PreHardmodeAP} 护甲穿透]";

            tooltips.Insert(2, new TooltipLine(Mod, "CollectorStats", statsLine));
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.GoldBar, 15)  // 15 个金锭
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
