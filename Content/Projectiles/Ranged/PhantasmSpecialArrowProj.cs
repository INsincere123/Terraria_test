using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using ProjectileTracking = TestMod.Common.GlobalProjectiles.GlobalProjectile;
using TestMod.Common.Mechanics.ArmorShred;
using TestMod.Content.Buffs;

namespace TestMod.Content.Projectiles.Ranged
{
    /// <summary>
    /// 幻影弓强化专用弹射物
    /// 特性：穿墙 / 无线穿透 / 平滑追踪 / 破甲debuff / 独立无敌帧防骗伤
    /// 触发原版幻影箭：每帧设置 player.phantasmTime = 2
    /// </summary>
    public class PhantasmSpecialArrowProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_935"; // 借用夜明箭贴图

        public const int TimeLeft = (int)(60 * 5.4f);   // 存活更新次数（每游戏帧两次，约 2.7 秒）
        public const int TrackingDelay = 6;              // 起飞延迟阈值（AI 更新次数）

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type]     = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width       = 10;        // 碰撞箱宽（像素）
            Projectile.height      = 10;        // 碰撞箱高（像素）
            Projectile.friendly    = true;      // 伤害敌人而非玩家
            Projectile.DamageType  = DamageClass.Ranged;    // 受远程加成影响
            Projectile.arrow       = true;      // 标记为箭矢，激活幻影弓等弓的特殊逻辑
            Projectile.tileCollide = false;     // 穿墙
            Projectile.penetrate   = -1;        // 无限穿透
            Projectile.timeLeft    = TimeLeft;
            Projectile.light       = 0.5f;      // 发出微弱光晕（范围 0~1）
            Projectile.extraUpdates = 1;        // 每帧更新 2 次（速度加倍，追踪更流畅）

            // 独立无敌帧，多支箭同时命中同一敌人各自结算，不互相骗伤
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown  = 5;
        }

        // ══════════════════════════════════════════════════════════════
        //   AI — 每帧触发幻影箭 + 平滑追踪 + 粒子尾迹
        // ══════════════════════════════════════════════════════════════
        public override void AI()
        {
            // 速度上限：防止 shootSpeedMult 词缀倍率过大时初速失控
            // 保持略高于追踪稳定速度（25.3f），保留初速的"冲劲"感
            const float maxSpeed = 31.05f;
            float curSpeed = Projectile.velocity.Length();
            if (curSpeed > maxSpeed)
                Projectile.velocity = Projectile.velocity / curSpeed * maxSpeed;

            // 触发原版幻影箭生成逻辑（参考灾厄 RiftburstBow）
            Main.player[Projectile.owner].phantasmTime = 2;

            // 贴图朝向修正（夜明箭贴图竖直，需要 +PiOver2）
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // ai[0] 是命中后的不追踪冷却计时器
            if (Projectile.ai[0] > 0)
            {
                Projectile.ai[0]--;
            }
            else if (Projectile.timeLeft < TimeLeft - TrackingDelay) // 沿用原起飞阈值，保留散射方向
            {
                int targetIndex = ProjectileTracking.FindTrackingTarget(Projectile, Projectile.Center, 3000f);
                if (targetIndex >= 0)
                {
                    NPC target = Main.npc[targetIndex];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float dist = toTarget.Length();

                    if (dist > 1f)
                    {
                        toTarget.Normalize();

                        const float desiredSpeed = 25.3f;
                        const float lerpAmount   = 0.15f; // 平滑转向，不急转

                        Projectile.velocity = Vector2.Lerp(
                            Projectile.velocity,
                            toTarget * desiredSpeed,
                            lerpAmount);

                        // 速度保底，防止 Lerp 后速度被拉低
                        if (Projectile.velocity.LengthSquared() < desiredSpeed * desiredSpeed * 0.64f)
                            Projectile.velocity = Projectile.velocity.SafeNormalize(toTarget) * (desiredSpeed * 0.8f);
                    }
                }
            }


        }

        // ══════════════════════════════════════════════════════════════
        //   OnHitNPC — 破甲debuff叠加 + 触发冷却
        //   保留本体命中与破甲；不再触发链式跳跃或范围爆炸。
        // ══════════════════════════════════════════════════════════════
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 命中后 10 次 AI 更新不追踪，让箭矢自然穿过去。
            Projectile.ai[0] = 10f;
            Projectile.netUpdate = true;

            // 破甲debuff叠加（最多10层）
            if (ArmorShredSystem.GetStacks(target.whoAmI) < ArmorShredSystem.MaxStacks)
                ArmorShredSystem.AddStack(target.whoAmI);

            target.AddBuff(ModContent.BuffType<ArmorShredDebuff>(), 180);
        }


    }
}
