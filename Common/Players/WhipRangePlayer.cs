using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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

            if (Player.GetModPlayer<GodModePlayer>().GodModeBuff && held.type == ItemID.RainbowWhip)
                Player.whipRangeMultiplier *= 1.5f;
        }
    }
}
