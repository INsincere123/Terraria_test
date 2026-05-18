using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Items.Accessories
{
    public class HeavenlyCrown : DeathBoundItem
    {
        //public override string Texture => "Terraria/Images/Item_" + ItemID.GoldCrown;

        public override void SetDefaults()
        {
            Item.width     = 24;
            Item.height    = 24;
            Item.accessory = true;
            Item.rare      = ItemRarityID.Yellow;
            Item.value     = Item.sellPrice(gold: 20);
        }

        public override string BreakMessage => "天赐华冠已破碎";

        protected override void UpdateEffect(Player player, bool hideVisual)
        {
            player.GetModPlayer<CorePlayer>().independentDamageMult *= 1.3f;
        }
    }
}
