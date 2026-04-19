using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace 武器test.Projectiles
{
    public class PhantasmHoldout : ModProjectile
    {
        // 直接复用原版幻影弓贴图
        public override string Texture => "Terraria/Images/Item_" + ItemID.Phantasm;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 62;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.hide = false;
        }

        // ai[0] = 存在计时器
        // ai[1] = 射击冷却计时器

        private const int FireDelay = 12; // 与 useTime 一致

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            // 玩家不能继续使用就销毁
            if (!player.active || player.dead || player.noItems || player.CCed
            || !player.HasAmmo(player.HeldItem)
            || player.HeldItem.type != ItemID.Phantasm
            || !player.channel)  // 松开鼠标时 channel 为 false
            {
                Projectile.Kill();
                return;
            }

            Projectile.ai[0] += 1f;

            // 跟随玩家手部位置
            Vector2 mountedCenter = player.RotatedRelativePoint(player.MountedCenter, true);
            Vector2 toMouse = Main.MouseWorld - mountedCenter;
            toMouse.Normalize();

            Projectile.velocity = toMouse * 0.1f; // 极小速度只用于确定朝向
            Projectile.position = mountedCenter - Projectile.Size / 2f;
            Projectile.rotation = toMouse.ToRotation() + (Projectile.spriteDirection == -1 ? MathHelper.Pi : 0f);
            Projectile.spriteDirection = Projectile.direction;
            Projectile.timeLeft = 2;

            player.ChangeDir(Projectile.direction);
            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.itemRotation = (float)Math.Atan2(
                toMouse.Y * Projectile.direction,
                toMouse.X * Projectile.direction);

            // 射击冷却
            Projectile.ai[1] -= 1f;
            if (Projectile.ai[1] > 0f) return;
            Projectile.ai[1] = FireDelay;

            if (Main.myPlayer != Projectile.owner) return;

            // 拾取弹药
            player.PickAmmo(player.HeldItem, out _, out float shootSpeed,
                out int arrowDamage, out float arrowKnockback, out _);

            float spread = MathHelper.ToRadians(6f);
            int arrowCount = 2;
            Vector2 baseVel = toMouse * shootSpeed;

            SoundEngine.PlaySound(SoundID.Item5, Projectile.position);

            for (int i = 0; i < arrowCount; i++)
            {
                float offsetAngle = spread * (i - (arrowCount - 1f) / 2f);
                Vector2 vel = baseVel.RotatedBy(offsetAngle) * Main.rand.NextFloat(0.9f, 1.1f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    mountedCenter, vel,
                    ModContent.ProjectileType<PhantasmSpecialArrowProj>(),
                    arrowDamage, arrowKnockback, Projectile.owner);
            }
        }

        public override bool? CanDamage() => false;
    }
}