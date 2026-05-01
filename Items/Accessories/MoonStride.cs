using TestMod.Common.Players;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System.Collections.Generic;
using TestMod.Rarities;
using System.Linq;
using TestMod.Buffs;

namespace TestMod.Items.Accessories
{
    public class MoonStride : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.defense = 10;
            Item.value = Item.sellPrice(platinum: 2);
            Item.rare = ModContent.RarityType<AntaresRarity>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 精确飞行（按 G 开关，按住 RightShift 减速）
            player.GetModPlayer<PreciseFlightPlayer>().allowToggle = true;

            // 防击退
            player.noKnockback = true;

            // 减少 25% 弹幕伤害（独立乘区）
            player.GetModPlayer<CorePlayer>().projDamageMultiplier = 0.75f;

            player.moveSpeed += 0.15f; // 增加 15% 移动速度

            player.AddBuff(ModContent.BuffType<GravityNormalizerBuff>(), 2);  // 重力正常化 buff
        }


        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string toggleKey = PreciseFlightKeybinds.ToggleHotkey.GetAssignedKeys().FirstOrDefault() ?? "未绑定";
            string slowKey = PreciseFlightKeybinds.SlowdownHotkey.GetAssignedKeys().FirstOrDefault() ?? "未绑定";

            foreach (TooltipLine line in tooltips)
            {
                if (line.Text.Contains("[TOGGLE]") || line.Text.Contains("[SLOW]"))
                {
                    line.Text = line.Text
                        .Replace("[TOGGLE]", toggleKey)
                        .Replace("[SLOW]", slowKey);
                }
            }
        }
    }
}
