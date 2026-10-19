using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Accessories
{
    public class SkywardSigil : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.accessory = true;
            Item.value = Item.sellPrice(gold: 15);
            Item.rare = ItemRarityID.Red;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
            => player.GetModPlayer<SkywardSigilPlayer>().HasSkywardSigil = true;
    }
}
