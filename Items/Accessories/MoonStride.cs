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

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe(); 
            recipe.AddIngredient(ItemID.RocketBoots);          // 火箭靴 128
            recipe.AddIngredient(ItemID.EmpressFlightBooster); // 翱翔之证 4989
            // ── 翅膀材料 ──
            recipe.AddIngredient(ItemID.AngelWings);           // 天使之翼 493
            recipe.AddIngredient(ItemID.DemonWings);           // 恶魔之翼 492
            recipe.AddIngredient(ItemID.FairyWings);           // 仙灵之翼 761
            recipe.AddIngredient(ItemID.FrozenWings);          // 冰冻之翼 822
            recipe.AddIngredient(ItemID.HarpyWings);           // 鸟妖之翼 785
            recipe.AddIngredient(ItemID.Jetpack);              // 喷气背包 748
            recipe.AddIngredient(ItemID.LeafWings);            // 叶之翼 1162
            recipe.AddIngredient(ItemID.BatWings);             // 蝙蝠之翼 1165
            recipe.AddIngredient(ItemID.BeeWings);             // 蜜蜂之翼 1515
            recipe.AddIngredient(ItemID.ButterflyWings);       // 蝴蝶之翼 749
            recipe.AddIngredient(ItemID.FlameWings);           // 烈焰之翼 821
            recipe.AddIngredient(ItemID.Hoverboard);           // 悬浮板 1866
            recipe.AddIngredient(ItemID.BoneWings);            // 骨之翼 786
            recipe.AddIngredient(ItemID.MothronWings);         // 蛾怪之翼 2770
            recipe.AddIngredient(ItemID.GhostWings);           // 幽灵之翼 823
            recipe.AddIngredient(ItemID.BeetleWings);          // 甲虫之翼 2280
            recipe.AddIngredient(ItemID.FestiveWings);         // 喜庆之翼 1871
            recipe.AddIngredient(ItemID.SpookyWings);          // 阴森之翼 1830
            recipe.AddIngredient(ItemID.TatteredFairyWings);   // 褴褛仙灵之翼 1797
            recipe.AddIngredient(ItemID.SteampunkWings);       // 蒸汽朋克之翼 948
            recipe.AddIngredient(ItemID.BetsyWings);           // 双足翼龙之翼 3883
            recipe.AddIngredient(ItemID.RainbowWings);         // 女皇之翼 4823
            recipe.AddIngredient(ItemID.FishronWings);         // 猪龙鱼之翼 2609
            recipe.AddIngredient(ItemID.WingsNebula);          // 星云斗篷 3470
            recipe.AddIngredient(ItemID.WingsVortex);          // 星旋强化翼 3469
            recipe.AddIngredient(ItemID.WingsSolar);           // 日耀之翼 3468
            recipe.AddIngredient(ItemID.WingsStardust);        // 星尘之翼 3471
            recipe.AddIngredient(ItemID.LongRainbowTrailWings);       // 天界星盘 4954

            // ── 合成站（根据需要调整）──
            recipe.AddTile(TileID.LunarCraftingStation);               

            recipe.Register();
        }

    }
}
