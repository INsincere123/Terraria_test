using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => VortexAmmo_ModifyWeaponDamage(item, player, ref damage);
    }
}
