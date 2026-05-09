using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Items.Accessories.Effects;

namespace TestMod.Common.GlobalProjectiles
{
    /// <summary>
    /// 钩爪数值修改汇总（GlobalProjectile 层），GrappleRange射程无法修改
    /// Item 层（shootSpeed 等）在 TestGlobalItem.cs 修改
    /// </summary>
    public partial class TestGlobalProjectile : GlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   拉人速度
        // ══════════════════════════════════════════════════════════════
        public override void GrapplePullSpeed(Projectile projectile, Player player, ref float speed)
        {
            // 紫晶钩 — Bobbit Hook 数据：24（原版 11）
            if (projectile.type == ProjectileID.GemHookAmethyst)
                speed = 24f;

            // 红木魔石效果：所有钩爪拉人速度 ×1.5
            if (player.GetModPlayer<OmniEffectsPlayer>().EnableGrapple)
                speed *= 1.5f;
        }

        // ══════════════════════════════════════════════════════════════
        //   缩回速度
        // ══════════════════════════════════════════════════════════════
        public override void GrappleRetreatSpeed(Projectile projectile, Player player, ref float speed)
        {
            // 紫晶钩 — Bobbit Hook 数据：28（原版 11）
            if (projectile.type == ProjectileID.GemHookAmethyst)
                speed = 28f;

            // 红木魔石效果：所有钩爪缩回速度 ×3
            if (player.GetModPlayer<OmniEffectsPlayer>().EnableGrapple)
                speed *= 3f;
        }
    }
}
