using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Accessories
{
    public sealed class TerraShield : ModItem
    {
        public const int ExtraDodgeChanceDenominator = 4;
        public const int ExtraDodgeCooldownTicks = 30 * 60;
        public const int ExtraDebuffTimeReduction = 1;

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.defense = 50;
            Item.rare = ModContent.RarityType<TerraRarity>();
            Item.value = Item.sellPrice(gold: 20);
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) =>
            !IsChalice(equippedItem) && !IsChalice(incomingItem);

        internal static bool IsChalice(Item item) => item.ModItem?.Mod.Name == "CalamityMod" &&
            item.ModItem.Name == "ChaliceOfTheBloodGod";

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<TerraShieldPlayer>().ApplyAccessory(Item);
            SuperEnergyShieldItem.ApplyEffects(player);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            TooltipLine limit = tooltips.Find(line => line.Mod == "Terraria" && line.Name == "Tooltip5");
            if (limit != null)
            {
                int percent = !Main.expertMode ? 20 : Main.masterMode ? 40 : 30;
                limit.Text = Language.GetTextValue("Mods.TestMod.TerraShieldEffects.DamageLimit", percent);
            }
            SuperEnergyShieldItem.AddShieldTooltip(tooltips, Mod);
        }

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.AnkhShield)
            .AddIngredient(ItemID.MasterNinjaGear)
            .AddIngredient(ItemID.BeeCloak)
            .AddIngredient(ItemID.CelestialShell)
            .AddIngredient(ItemID.FrozenShield)
            .AddIngredient(ItemID.HeroShield)
            .AddIngredient(ItemID.StarVeil)
            .AddIngredient(ItemID.BrainOfConfusion)
            .AddIngredient(ItemID.WormScarf)
            .AddIngredient(ItemID.ShinyStone)
            .AddIngredient(ItemID.CharmofMyths)
            .AddIngredient<SuperEnergyShieldItem>()
            .AddTile(TileID.LunarCraftingStation)
            .Register();
    }
}
