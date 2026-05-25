using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.GlobalProjectiles;
using TestMod.Content.Items.Accessories.Effects;

namespace TestMod.Content.Projectiles.Accessories
{
    // ============================================================================
    //  ReflectShieldOrb  ——  反射护盾弹幕
    // ----------------------------------------------------------------------------
    //  ai[1] = 弹幕在护盾中的索引
    //
    //  运作流程：
    //    每帧定位 → Hitbox 与敌方射弹碰撞检测
    //    → ShouldReflect=true：改归属 + 反转速度 + 倍增伤害
    //    → ShouldReflect=false：直接 Kill 射弹
    //    → 扣自身耐久，归零则自毁（方案A）
    //
    //  过滤规则：
    //    - 速度为零的静止型射弹直接消除（无法有效反弹）
    //    - 已被标记过的射弹跳过（AlreadyReflected）
    // ============================================================================

    public class ReflectShieldOrb : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_12";

        private float _currentHP = -1f;

        public override void SetDefaults()
        {
            Projectile.width       = 28;
            Projectile.height      = 28;
            Projectile.friendly    = true;
            Projectile.hostile     = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate   = -1;
            Projectile.timeLeft    = 2;
            Projectile.damage      = 0;
            Projectile.DamageType  = DamageClass.Generic;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            var mp = owner.GetModPlayer<OmniEffectsPlayer>();
            if (!mp.EnableReflectShield)
            {
                Projectile.Kill();
                return;
            }

            if (_currentHP < 0f)
                _currentHP = mp.ReflectShieldConfig.OrbHP;

            Projectile.timeLeft = 2;

            // ── 轨道定位 ──────────────────────────────────────────────
            int   idx   = (int)Projectile.ai[1];
            float angle = mp.ReflectShieldRotationAngle
                        + MathHelper.TwoPi / mp.ReflectShieldConfig.OrbCount * idx;
            Projectile.Center   = owner.MountedCenter
                                + new Vector2(mp.ReflectShieldConfig.OrbRadius, 0f).RotatedBy(angle);
            Projectile.velocity = Vector2.Zero;

            // ── 碰撞检测与拦截 ────────────────────────────────────────
            ScanAndReflect(owner, mp);
        }

        private void ScanAndReflect(Player owner, OmniEffectsPlayer mp)
        {
            Rectangle orbHitbox = Projectile.Hitbox;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.hostile || proj.damage <= 0) continue;

                var gp = proj.GetGlobalProjectile<global::TestMod.Common.GlobalProjectiles.GlobalProjectile>();
                if (gp.AlreadyReflected) continue;

                // 碰撞判定：Hitbox 交叉 或 中心距离 < 双方半径之和
                bool overlaps = orbHitbox.Intersects(proj.Hitbox)
                             || Vector2.Distance(proj.Center, Projectile.Center)
                                < Projectile.width * 0.5f + proj.width * 0.5f;
                if (!overlaps) continue;

                // 标记，防止同帧其他球或下帧重复处理
                gp.AlreadyReflected = true;

                // 方案A：先完整处理，再判断是否破碎
                _currentHP -= MitigatedDamage(proj.damage, owner);

                if (mp.ReflectShieldConfig.ShouldReflect && proj.velocity != Vector2.Zero)
                {
                    // 反转归属 + 速度 + 伤害
                    proj.hostile  = false;
                    proj.friendly = true;
                    proj.owner    = owner.whoAmI;
                    proj.velocity = -proj.velocity;
                    proj.damage   = (int)(proj.damage * mp.ReflectShieldConfig.ReflectDamageMult);
                }
                else
                {
                    // 速度为零的静止弹幕 或 ShouldReflect=false：直接消除
                    proj.Kill();
                }

                if (_currentHP <= 0)
                {
                    mp.ReflectShieldRespawnCooldown = mp.ReflectShieldConfig.RespawnCooldown;
                    Projectile.Kill();
                    return;
                }
            }
        }

        public override bool? CanHitNPC(NPC target) => false;

        // 原版伤害公式：raw - defense/2（最低1），再乘免伤系数
        private static float MitigatedDamage(int rawDamage, Player owner)
        {
            float afterDefense   = System.Math.Max(1f, rawDamage - owner.statDefense / 2f);
            float afterEndurance = afterDefense * (1f - owner.endurance);
            return System.Math.Max(1f, afterEndurance);
        }
    }
}
