using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Projectiles
{
    /// <summary>
    /// 测试鞭子 tag 效果触发的爆炸弹射物。
    /// 召唤物打到 tag 敌人时在其中心生成，对周围敌人造成 AoE 伤害。
    /// </summary>
    public class TestWhipExplosionProj : ModProjectile
    {
        // PreDraw 返回 false，不显示任何贴图，随便指一个存在的贴图占位
        public override string Texture => "Terraria/Images/Projectile_1";

        public override void SetDefaults()
        {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;          // 穿透所有敌人
            Projectile.timeLeft = 3;            // 只存在 3 帧，足够命中
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            // 只在第一帧播放音效和生成粒子
            if (Projectile.timeLeft == 3)
            {
                SoundEngine.PlaySound(SoundID.Item14, Projectile.Center); // 爆炸音效

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 25; i++)
                    {
                        Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                        Dust d = Dust.NewDustDirect(
                            Projectile.position, Projectile.width, Projectile.height,
                            DustID.Torch, vel.X, vel.Y, 0, default, 1.8f);
                        d.noGravity = true;
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
                        Dust d = Dust.NewDustDirect(
                            Projectile.position, Projectile.width, Projectile.height,
                            DustID.Smoke, vel.X, vel.Y, 100, default, 1.2f);
                        d.noGravity = false;
                    }
                }
            }
        }

        // 无贴图，纯粒子效果
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
