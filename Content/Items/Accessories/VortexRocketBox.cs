using Terraria.ID;

namespace TestMod.Content.Items.Accessories
{
    public sealed class VortexRocketBox : VortexAmmoAccessory
    {
        public override int AmmoCategory => AmmoID.Rocket;
        protected override int Ingredient => ItemID.RocketLauncher;
        public override string Texture => "TestMod/Assets/Textures/Items/Accessories/VortexRocketBox";
        // 火箭武器和弹药伤害经 GlobalItem 处理，不改 specialistDamage。
    }
}
