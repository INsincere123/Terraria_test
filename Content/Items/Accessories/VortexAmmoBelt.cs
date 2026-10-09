using Terraria;
using Terraria.ID;

namespace TestMod.Content.Items.Accessories
{
    public sealed class VortexAmmoBelt : VortexAmmoAccessory
    {
        public override int AmmoCategory => AmmoID.Bullet;
        protected override int Ingredient => ItemID.AmmoBox;
        public override string Texture => "TestMod/Assets/Textures/Items/Accessories/VortexAmmoBelt";
        protected override void ApplyAmmoStats(Player player) => player.bulletDamage *= 1.1f;
    }
}
