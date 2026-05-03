using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Projectiles.Accessories
{
    /// <summary>
    /// 副手炮台弹幕。常驻于玩家头顶，锁定目标后发射 MagnetSphereBolt。
    /// 本体贴图占位：MagnetSphereBall（ProjectileID.MagnetSphereBall）。
    ///
    /// ai[0]：目标 NPC 的 whoAmI + 1（0 = 无目标）。
    /// ai[1]：无用，保留。
    /// localAI[0]：旋转角度（纯视觉）。
    /// </summary>
    public class SubhandCannon : ModProjectile
    {
        // ======================================================
        //  调参区
        // ======================================================

        /// <summary>炮台相对玩家头部的偏移（像素）。</summary>
        private static readonly Vector2 CannonOffset = new Vector2(0f, -40f);

        /// <summary>发射子弹幕的速度（像素/帧）。</summary>
        private const float BoltSpeed = 14f;

        /// <summary>空闲时的旋转速度（弧度/帧）。</summary>
        private const float IdleRotationSpeed = 0.05f;

        // ======================================================
        //  属性快捷访问
        // ======================================================

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>当前锁定的目标 NPC（0 = 无目标）。</summary>
        private int TargetIndex
        {
            get => (int)Projectile.ai[0] - 1;
            set => Projectile.ai[0] = value + 1;
        }

        // ======================================================
        //  SetStaticDefaults / SetDefaults
        // ======================================================

        public override void SetStaticDefaults()
        {
            // 复用 MagnetSphereBall 的帧数设置
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.MagnetSphereBall];
        }

        public override void SetDefaults()
        {
            // 尺寸对齐 MagnetSphereBall
            Projectile.width = 22;
            Projectile.height = 22;

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;

            // 不造成伤害，只是视觉载体
            Projectile.damage = 0;
            Projectile.knockBack = 0f;

            // timeLeft 每帧在 AI 中重置，装备卸下后自然消失
            Projectile.timeLeft = 2;
        }

        // ======================================================
        //  AI
        // ======================================================

        public override void AI()
        {
            // 只在本地玩家端执行逻辑
            if (Projectile.owner != Main.myPlayer)
                return;

            // 跟随玩家头顶
            Projectile.Center = Owner.Top + CannonOffset;
            Projectile.velocity = Vector2.Zero;

            // 目标有效性检测
            NPC target = TargetIsValid() ? Main.npc[TargetIndex] : null;

            if (target != null)
            {
                // 朝目标旋转（视觉）
                Vector2 toTarget = target.Center - Projectile.Center;
                Projectile.localAI[0] = toTarget.ToRotation();
            }
            else
            {
                // 无目标，空转
                Projectile.localAI[0] += IdleRotationSpeed;
                Projectile.ai[0] = 0f; // 清空目标
            }

            // 动画帧
            if (++Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[ProjectileID.MagnetSphereBall];
            }
        }

        // ======================================================
        //  公开接口：由 OnHitEffect.Trigger 调用
        // ======================================================

        /// <summary>
        /// 设置新目标并立即从炮台位置向目标发射一颗 MagnetSphereBolt。
        /// 仅在服务端/单机端调用（Projectile.owner == Main.myPlayer 已由调用方保证）。
        /// 想用别的弹幕: cannon?.FireAt(target, (int)(damageDone * DAMAGE_RATIO), ProjectileID.xxx);
        /// </summary>
        public void FireAt(NPC target, int damage, int boltType = ProjectileID.MagnetSphereBolt)
        {
            TargetIndex = target.whoAmI;
            Projectile.netUpdate = true;

            Vector2 velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * BoltSpeed;

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                velocity,
                boltType,
                damage,
                2f,
                Projectile.owner
            );
        }

        // ======================================================
        //  检查目标是否仍然有效
        // ======================================================

        private bool TargetIsValid()
        {
            if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs)
                return false;
            NPC npc = Main.npc[TargetIndex];
            return npc.active && !npc.friendly && npc.life > 0;
        }

        /// <summary>当前使用的贴图路径，由生成方传入。默认为 MagnetSphereBall。</summary>
        public string TextureOverride = null;
        public override string Texture => TextureOverride ?? $"Terraria/Images/Projectile_{ProjectileID.MagnetSphereBall}";
    }
}
