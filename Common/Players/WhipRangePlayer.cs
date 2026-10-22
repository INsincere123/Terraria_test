using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.Weapons.Plus;

namespace TestMod.Common.Players
{
    public class WhipRangePlayer : ModPlayer
    {
        public override void PostUpdateEquips()
        {
            Item held = Player.HeldItem;

            if (held.DamageType == DamageClass.SummonMeleeSpeed
                && held.shoot > ProjectileID.None
                && ProjectileID.Sets.IsAWhip[held.shoot]
                && held.scale != 1f)
            {
                Player.whipRangeMultiplier *= held.scale;
            }

            if (held.ModItem is KaleidoscopePlus)
                Player.whipRangeMultiplier *= 1.5f;
        }
    }
}
