using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalItems
{
    public class TerraArmorGlobalItem : Terraria.ModLoader.GlobalItem
    {
        public override bool CanConsumeAmmo(Item weapon, Item ammo, Player player) =>
            !player.GetModPlayer<TerraArmorPlayer>().IsMode(TerraArmorMode.Ranger);
    }
}
