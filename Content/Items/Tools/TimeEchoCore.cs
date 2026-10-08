using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using System.Collections.Generic;
using Terraria.Localization;
using TestMod.Common.Systems;

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
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            var keys = TimeEchoSystem.AttackKey?.GetAssignedKeys();
            string key = keys is { Count: > 0 } ? string.Join(" / ", keys) : Language.GetTextValue("Mods.TestMod.TimeEcho.Unbound");
            tooltips.Add(new TooltipLine(Mod, "TimeEchoAttackKey", Language.GetTextValue("Mods.TestMod.TimeEcho.AttackHint", key)));
            tooltips.Add(new TooltipLine(Mod, "TimeEchoAttack", Language.GetTextValue("Mods.TestMod.TimeEcho.AttackDetails")));
        }
        public override bool ConsumeItem(Player player) => Main.netMode == NetmodeID.SinglePlayer;
        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                player.GetModPlayer<TimeEchoPlayer>().RequestAbilityItem(true);
            return true;
        }
    }
}
