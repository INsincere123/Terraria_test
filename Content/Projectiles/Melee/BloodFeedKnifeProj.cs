using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Content.Items.DamageTypes;
using Microsoft.Xna.Framework;
using ProjectileTracking = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Content.Projectiles.Melee
{
    // ============================================================================
    //  BloodFeedKnifeProj  ——  血饲匕首弹幕
    // ----------------------------------------------------------------------------
    //  ai[0] = 命中后的停追冷却（AI 更新次数；初始 -1 表示无冷却）
    //  存活时间由 timeLeft 管理，目标由每实体 GlobalProjectile 缓存。
    //
    //  追踪逻辑：
    //    每 6 个游戏帧重选范围内最近敌人，无视墙壁；失效立即重选。
    //    每次 AI 更新插值转向目标，保留既有稳定速度和速度保底。
    //
    //  伤害逻辑：
    //    通过 BloodFeedPlayer 读取当前阶段伤害倍率并应用到 ModifyHitNPC
    // ============================================================================
    public class BloodFeedKnifeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_497";

        // ── 数值调节区 ────────────────────────────────────────────────
        // 追踪参数在本类 AI 内调整；extraUpdates = 1，每个游戏帧更新两次。
        public const int   Lifetime        = 240;   // 最大存活更新次数（约 2 秒）
        public const float RotationOffset  = MathHelper.PiOver2; // 贴图朝向修正（顺时针90°）
        public const int TrackingDelay      = 17;     // 起飞延迟阈值（AI 更新次数，沿用现有时长）
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

            Projectile.ai[0] = -1f; // 无停追冷却
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
                int targetIndex = ProjectileTracking.FindTrackingTarget(Projectile, Projectile.Center, 3000f);
                if (targetIndex >= 0)
                {
                    NPC target = Main.npc[targetIndex];
                    Vector2 toTarget = target.Center - Projectile.Center;
                    float dist = toTarget.Length();

                    if (dist > 1f)
                    {
                        toTarget.Normalize();

                        const float desiredSpeed = 23f;
                        const float lerpAmount   = 0.2375f; // 平滑转向，不急转

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
            // 命中后 10 次 AI 更新不追踪，自然穿过去。
            Projectile.ai[0] = 10f;
            Projectile.netUpdate = true;
        }

    }
}
