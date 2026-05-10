using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Items.DamageTypes;

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
        public static int   Lifetime        = 180;   // 最大存活帧数（3秒）
        public static float RotationSpeed   = 0.45f; // 自转速度（rad/帧）
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
            Projectile.extraUpdates = 0;

            Projectile.ai[0] = -1f; // 未锁定状态
        }

        public override void AI()
        {
            // 自转（视觉）——追踪逻辑由 TestGlobalProjectile.PostAI 的分发统一处理
            Projectile.rotation += RotationSpeed;
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
    }
}
