using Terraria;
using Terraria.ID;

namespace TestMod.Content.Items.Accessories
{
    public sealed class VortexQuiver : VortexAmmoAccessory
    {
        public override int AmmoCategory => AmmoID.Arrow;
        protected override int Ingredient => ItemID.MagicQuiver;
        public override string Texture => "TestMod/Assets/Textures/Items/Accessories/VortexQuiver";
        protected override void ApplyAmmoStats(Player player)
        {
            // 原版箭袋的箭速、击退与节省箭矢判定；伤害倍率单独保留。
            player.magicQuiver = true;
            player.arrowDamage *= 1.1f;
        }
    }
}
