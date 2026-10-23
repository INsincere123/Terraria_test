using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Accessories
{
    public sealed class SpiritGuidanceCore : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = Item.height = 30;
            Item.accessory = true;
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
            => player.GetModPlayer<SpiritGuidancePlayer>().Enabled = true;
    }
}
