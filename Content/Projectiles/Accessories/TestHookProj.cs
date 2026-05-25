using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.Accessories;

namespace TestMod.Content.Projectiles.Accessories
{
    public class TestHookProj : ModProjectile
    {
        // 复用圣诞钩弹射物贴图（ProjectileID 332）
        public override string Texture => "Terraria/Images/Projectile_332";

        public override void SetDefaults()
        {
            Projectile.CloneDefaults(ProjectileID.ChristmasHook);
        }

        public override float GrappleRange()
            => TestHook.Reach * 16f;

        public override void NumGrappleHooks(Player player, ref int numHooks)
            => numHooks = TestHook.MaxHooks;

        public override void GrapplePullSpeed(Player player, ref float speed)
            => speed = TestHook.PullSpeed;

        public override void GrappleRetreatSpeed(Player player, ref float speed)
            => speed = TestHook.ReelbackSpeed;
    }
}
