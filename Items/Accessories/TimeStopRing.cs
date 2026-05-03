using System.Collections.Generic;
using TestMod.Common.Systems;
using TestMod.Items.Accessories.Effects;
using TestMod.Items.Accessories.Effects;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Items.Accessories
{
    /// <summary>
    /// 时停戒指 —— 测试用饰品，装备后可按绑定键触发时停。
    /// 时停期间所有敌方 NPC 和敌方弹幕完全静止，玩家不受影响。
    /// 持续 3 秒，冷却 30 秒。
    /// </summary>
    public class TimeStopRing : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 22;
            Item.accessory = true;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.sellPrice(gold: 5);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            TimeStopEffect.Grant(player);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string keyText = KeybindUtils.GetKeyText(TimeStopKeybinds.TimeStopKey);
            tooltips.Add(new TooltipLine(Mod, "TimeStopKey", $"按 [{keyText}] 触发时停"));
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.GoldBar, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}