using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Content.Buffs;

namespace TestMod.Content.Items.Potions
{
    public class ClassicGrapeBeer : ModItem
    {
        public const int Duration = 6 * 60 * 60;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 20;
            ItemID.Sets.DrinkParticleColors[Type] = new[] {
                new Color(36, 2, 41), new Color(56, 0, 64), new Color(82, 10, 92)
            };
        }

        public override void SetDefaults()
        {
            Item.DefaultToFood(12, 28, ModContent.BuffType<ClassicGrapeBeerBuff>(), Duration, true);
            Item.value = Item.sellPrice(silver: 2);
            Item.rare = ItemRarityID.LightRed;
        }

        public override bool? UseItem(Player player)
        {
            ClassicGrapeBeerSystem.RemoveOriginalBuff(player);
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe(10).AddIngredient(ItemID.Bottle, 10).AddIngredient(ItemID.Grapes)
                .AddTile(TileID.Kegs).Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            if (ModLoader.HasMod("CalamityMod"))
                tooltips.Add(new TooltipLine(Mod, "CalamityCompatibility",
                    Language.GetTextValue("Mods.TestMod.Items.ClassicGrapeBeer.CalamityCompatibility")));
        }
    }
}
