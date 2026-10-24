using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Accessories
{
    public sealed class SummonOverdriveCore : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = Item.height = 30;
            Item.accessory = true;
            Item.rare = ModContent.RarityType<TerraRarity>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<SummonOverdrivePlayer>().Enabled = true;
            SummonCritPlayer.Enable(player);
            player.GetModPlayer<SummonCritPlayer>().BonusCritChance += 1f;
        }
    }
}
