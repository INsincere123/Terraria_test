using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        // 射手之证独立省弹：20% 概率不消耗弹药，与弹药箱 Buff 各自掷骰、可叠加。
        public override bool CanConsumeAmmo(Item weapon, Item ammo, Player player)
        {
            if (player.TryGetModPlayer(out VortexQuiverPlayer quiver) &&
                quiver.HasRangerSeal && Main.rand.NextBool(5))
                return false;
            return true;
        }

        private static void VortexAmmo_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            // 星璇火箭箱独立提供伤害，射手之证不占用其装备状态。
            if (item.useAmmo == AmmoID.Rocket && player.TryGetModPlayer(out VortexQuiverPlayer ammo) &&
                ammo.IsEquipped(AmmoID.Rocket))
                damage *= 1.1f;
        }

        public override void PickAmmo(Item weapon, Item ammo, Player player, ref int type,
            ref float speed, ref StatModifier damage, ref float knockback)
        {
            if (!player.TryGetModPlayer(out VortexQuiverPlayer state)) return;

            // 熔火箭袋功能由原版字段处理，不在 PickAmmo 再次放大速度。
            if (weapon.useAmmo == AmmoID.Rocket && state.IsEquipped(AmmoID.Rocket))
            {
                // Flat 已含修正过的武器伤害；乘算只处理弹药贡献，不能再次乘 Flat。
                damage *= 1.1f;
            }
        }
    }
}
