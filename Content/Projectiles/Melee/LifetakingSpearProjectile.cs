using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Projectiles.Melee
{
    public sealed class LifetakingSpearProjectile : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Daybreak}";

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 3600;
            Projectile.alpha = 255;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => Projectile.ai[0] == 0 ? null : false;
        public override bool? CanHitNPC(NPC target) => Projectile.ai[0] == 0 ? null : false;

        public override void AI()
        {
            if (!Main.player[Projectile.owner].active || Main.player[Projectile.owner].dead)
            { Projectile.Kill(); return; }
            Projectile.alpha = System.Math.Max(0, Projectile.alpha - 25);
            if (Projectile.ai[0] != 0)
            {
                Player owner = Main.player[Projectile.owner];
                var state = owner.GetModPlayer<LifetakingPlayer>();
                if (!owner.active || owner.dead || !state.TryGetTarget(out NPC target)
                    || target.whoAmI != (int)Projectile.ai[1])
                {
                    // 命中包/状态包可能比弹幕附着包晚到，等待确认只影响外观，不投送伤害。
                    if (owner.active && !owner.dead && Main.netMode != NetmodeID.SinglePlayer && ++Projectile.ai[2] <= 90)
                    { Projectile.velocity = Vector2.Zero; return; }
                    Projectile.Kill(); return;
                }
                Projectile.Center = target.Center - (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 20;
                Projectile.velocity = Vector2.Zero;
                Projectile.tileCollide = false;
                return;
            }
            // 原版破晓飞行：前45次AI保持初速，其后缓慢下坠。独立类型不套用追踪强化。
            if (++Projectile.ai[1] >= 45)
            {
                Projectile.ai[1] = 45;
                Projectile.velocity.X *= 0.995f;
                Projectile.velocity.Y += 0.15f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.7f, 0.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner != Main.myPlayer || Projectile.ai[0] != 0) return;
            var state = Main.player[Projectile.owner].GetModPlayer<LifetakingPlayer>();
            state.RequestMark(Projectile, target);
            Projectile.ai[0] = 1;
            Projectile.ai[1] = target.whoAmI;
            Projectile.timeLeft = LifetakingPlayer.MarkDuration * Projectile.MaxUpdates;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Main.EntitySpriteDraw(TextureAssets.Projectile[Type].Value, Projectile.Center - Main.screenPosition,
                null, Projectile.GetAlpha(lightColor), Projectile.rotation, TextureAssets.Projectile[Type].Value.Size() * 0.5f,
                Projectile.scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None);
            return false;
        }

        public override void OnKill(int timeLeft) => LifetakingAttackSystem.PlayFinish(Projectile.Center);
    }
}
