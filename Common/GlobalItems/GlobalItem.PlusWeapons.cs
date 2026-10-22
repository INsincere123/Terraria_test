using Terraria;
using Terraria.ID;
using TestMod.Common.Systems;
using TestMod.Content.Items.Weapons.Plus;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        private static void PlusWeapons_UseAnimation(Item item, Player player)
        {
            if ((item.type == ItemID.StardustDragonStaff || item.ModItem is StardustDragonStaffPlus) &&
                player.altFunctionUse != 2)
                PlusWeaponSummoning.PrepareDragon(player, item.ModItem is StardustDragonStaffPlus);
        }
    }
}
