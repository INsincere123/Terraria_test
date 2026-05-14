using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Items.DamageTypes;
using Microsoft.Xna.Framework;
using TestMod.Common.Utilities;

namespace TestMod.Projectiles.Melee
{
    // ============================================================================
    //  BloodFeedKnifeProj  ——  血饲匕首弹幕
    // ----------------------------------------------------------------------------
    //  ai[0] = 追踪目标 NPC 的 whoAmI（-1 = 尚未锁定）
    //  ai[1] = 存活计时器（超时自毁，防止永久漂浮）
    //
    //  追踪逻辑：
    //    每帧用 FindTargetWithLineOfSight 搜索范围内最近可见敌人
    //    将当前速度向量插值转向目标，速度大小保持不变
    //    转向速度由 TurnRate 控制（0 = 不转，1 = 瞬间对准）
    //
    //  伤害逻辑：
    //    通过 BloodFeedPlayer 读取当前阶段伤害倍率并应用到 ModifyHitNPC
    // ============================================================================
    public class BloodFeedKnifeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_497";

        // ── 数值调节区 ────────────────────────────────────────────────
        // 追踪参数在 TestGlobalProjectile.cs 的 PostAI 分发里调整：
        //   ApplyHighTierTracking(projectile, minSpeed:10f, maxSpeed:20f, lerpAmount:0.14f, extraCorrection:0.22f)
        public const int   Lifetime        = 240;   // 最大存活帧数（4秒）
        public const float RotationOffset  = MathHelper.PiOver2; // 贴图朝向修正（顺时针90°）
        public const int TrackingDelay      = 17;     // 生成后多少帧开始追踪
        // ─────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Projectile.width       = 22;
            Projectile.height      = 22;
            Projectile.friendly    = true;
            Projectile.hostile     = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate   = 1;
            Projectile.timeLeft    = Lifetime;
            Projectile.DamageType  = TrueDamageClass.Instance;
            Projectile.extraUpdates = 1;

            // 独立无敌帧，多支箭同时命中同一敌人各自结算，不互相骗伤
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown  = 10;

            Projectile.ai[0] = -1f; // 未锁定状态
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + RotationOffset;
            
            if (Projectile.ai[0] > 0)
            {
                Projectile.ai[0]--;
            }
            else if (Projectile.timeLeft < Lifetime - TrackingDelay)
            {
                int targetIndex =
                    TargetUtils.FindNearestTargetNotOnCooldown(
                        Projectile.Center,
                        3000f);
                if (targetIndex >= 0)
                {
                    NPC target = Main.npc[targetIndex];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float dist = toTarget.Length();

                    if (dist > 1f)
                    {
                        toTarget.Normalize();

                        const float desiredSpeed = 20f;
                        const float lerpAmount   = 0.19f; // 平滑转向，不急转

                        Projectile.velocity = Vector2.Lerp(
                            Projectile.velocity,
                            toTarget * desiredSpeed,
                            lerpAmount);

                        // 速度保底，防止 Lerp 后速度被拉低
                        float speed = Projectile.velocity.Length();
                        if (speed < desiredSpeed * 0.8f)
                            Projectile.velocity = Projectile.velocity / speed * (desiredSpeed * 0.8f);
                    }
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // 读取 BloodFeed 正常阶段伤害倍率（暴走/虚弱的通用加成已由 BloodFeedPlayer 注入 Generic）
            Player owner = Main.player[Projectile.owner];
            var mp = owner.GetModPlayer<BloodFeedPlayer>();
            float mult = mp.CurrentNormalMultiplier;
            if (mult > 1f)
                modifiers.SourceDamage *= mult;
        }

        // ══════════════════════════════════════════════════════════════
        //   OnHitNPC — 触发冷却
        // ══════════════════════════════════════════════════════════════
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 命中后10帧不追踪，自然穿过去
            Projectile.ai[0] = 10f;
        }

    }
}
