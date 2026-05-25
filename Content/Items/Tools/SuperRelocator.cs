using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Tools
{
    /// <summary>
    /// 超级定位器 — 升级版常态定位器，传送后可选择冻结或继承速度。
    /// 占位贴图：混沌传送杖，替换为自定义 PNG 后删除 Texture override。
    /// </summary>
    public class SuperRelocator : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.RodOfHarmony;

        public override void SetDefaults()
        {
            Item.width  = 28;
            Item.height = 28;
            Item.value  = Item.sellPrice(gold: 20);
            Item.rare   = ItemRarityID.Red;
        }

        // 在背包任意位置即可激活（同灾厄常态定位器）
        public override void UpdateInventory(Player player)
        {
            player.GetModPlayer<SuperRelocatorPlayer>().HasSuperRelocator = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // 动态显示当前绑定的快捷键
            var keys    = SuperRelocatorKeybind.Hotkey.GetAssignedKeys();
            string key  = keys.Count > 0 ? keys[0] : "?";
            tooltips.Add(new TooltipLine(Mod, "Hotkey",
                $"[{key}] 传送到鼠标位置"));
        }

        public override void AddRecipes()
        {
            // 灾厄加载时：常态定位器 + 5 魔影锭，嘉登熔炉
            if (CalamityCompatSystem.CalamityLoaded &&
                ModContent.TryFind<ModItem>("CalamityMod", "NormalityRelocator", out var nr) &&
                ModContent.TryFind<ModItem>("CalamityMod", "ShadowspecBar",      out var bar) &&
                ModContent.TryFind<ModTile>("CalamityMod", "DraedonsForge",      out var forge))
            {
                CreateRecipe()
                    .AddIngredient(nr.Type)
                    .AddIngredient(bar.Type, 5)
                    .AddTile(forge.Type)
                    .Register();
                return;
            }

            // 占位配方（无灾厄）：混沌传送杖 + 10 夜明锭
            CreateRecipe()
                .AddIngredient(ItemID.RodofDiscord)
                .AddIngredient(ItemID.LunarBar, 10)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}
