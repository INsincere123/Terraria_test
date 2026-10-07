using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Tools
{
    public sealed class TimeEchoCore : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = Item.height = 24;
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = true;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 30;
            Item.rare = ItemRarityID.Purple;
            Item.value = 0;
        }

        public override bool CanUseItem(Player player) => !player.GetModPlayer<TimeEchoPlayer>().HasAbility;
        public override bool ConsumeItem(Player player) => Main.netMode == NetmodeID.SinglePlayer;
        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                player.GetModPlayer<TimeEchoPlayer>().RequestAbilityItem(true);
            return true;
        }
    }
}
