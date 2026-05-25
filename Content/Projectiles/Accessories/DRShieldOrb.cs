using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.GlobalProjectiles;
using TestMod.Content.Items.Accessories.Effects;

namespace TestMod.Content.Projectiles.Accessories
{
    // 贴图复用 468 暗影火球（CultistBossFireBallClone）
    // ============================================================================
    //  DRShieldOrb  ——  伤害减免护盾弹幕
    // ----------------------------------------------------------------------------
    //  ai[0] 保留（未使用）
    //  ai[1] = 弹幕在护盾中的索引（0, 1, 2...），决定轨道角度偏移
    //
    //  运作流程：
    //    每帧定位到 owner 周围对应角度 → 扫描进入拦截半径的敌方射弹
    //    → 标记 MarkedForDRShield → 扣自身耐久
    //    → 耐久归零：设重生冷却，自毁（方案A：先完整处理再自毁）
    //
    //  减免实际执行在 GlobalProjectile.ShieldOrbs.cs 的 ModifyHitPlayer
    // ============================================================================

    public class DRShieldOrb : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_12";

        private float _currentHP = -1f; // -1 = 未初始化

        public override void SetDefaults()
        {
            Projectile.width       = 24;
            Projectile.height      = 24;
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
            if (!mp.EnableDRShield)
            {
                Projectile.Kill();
                return;
            }

            // 初始化耐久
            if (_currentHP < 0f)
                _currentHP = mp.DRShieldConfig.OrbHP;

            // 维持存活
            Projectile.timeLeft = 2;

            // ── 轨道定位 ──────────────────────────────────────────────
            int   idx   = (int)Projectile.ai[1];
            float angle = mp.DRShieldRotationAngle
                        + MathHelper.TwoPi / mp.DRShieldConfig.OrbCount * idx;
            Projectile.Center   = owner.MountedCenter
                                + new Vector2(mp.DRShieldConfig.OrbRadius, 0f).RotatedBy(angle);
            Projectile.velocity = Vector2.Zero;

            // ── 扫描并标记敌方射弹 ────────────────────────────────────
            ScanAndIntercept(owner, mp);
        }

        private void ScanAndIntercept(Player owner, OmniEffectsPlayer mp)
        {
            float r = mp.DRShieldConfig.DetectionRadius;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.hostile || proj.damage <= 0) continue;

                var gp = proj.GetGlobalProjectile<global::TestMod.Common.GlobalProjectiles.GlobalProjectile>();
                if (gp.MarkedForDRShield) continue; // 已被本帧或之前标记过

                if (Vector2.Distance(proj.Center, Projectile.Center) > r) continue;

                // 标记：命中玩家时 ModifyHitPlayer 将套用减免
                gp.MarkedForDRShield = true;

                // 方案A：先完整拦截，再判断是否破碎
                _currentHP -= MitigatedDamage(proj.damage, owner);
                if (_currentHP <= 0)
                {
                    mp.DRShieldRespawnCooldown = mp.DRShieldConfig.RespawnCooldown;
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
