using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Projectiles.Minions
{
    // ╔══════════════════════════════════════════════════════╗
    // ║          幻影龙单段统一类 · 参数调整区域             ║
    // ╠══════════════════════════════════════════════════════╣
    // ║  【伤害】                                            ║
    // ║    SegmentDamage      初始伤害（被Summoner覆盖）     ║
    // ║    SegmentKnockback   初始击退                       ║
    // ║    HitCooldown        每节独立无敌帧（帧）           ║
    // ╠══════════════════════════════════════════════════════╣
    // ║  【贴图】                                            ║
    // ║    FrameWidth/Height  单帧尺寸                       ║
    // ║    HeadRow/BodyRow/   头/身/尾在贴图中的行号         ║
    // ║    TailRow                                           ║
    // ╠══════════════════════════════════════════════════════╣
    // ║  【链结构】                                          ║
    // ║    SegmentDist        相邻节点中心距离（px）         ║
    // ║    RotationDamping    旋转阻尼（0~1，越小越柔顺）    ║
    // ╠══════════════════════════════════════════════════════╣
    // ║  【头节点 - Idle 悬浮】                              ║
    // ║    IdleOffsetX/Y      悬停目标相对玩家中心的偏移     ║
    // ║    HoverAccelFar      远距离逼近加速度（>200px）     ║
    // ║    HoverAccelMid      中距离逼近加速度（140~200px）  ║
    // ║    HoverAccelNear     近距离逼近加速度（<140px）     ║
    // ║    HoverDamping       接近后的速度衰减系数           ║
    // ║    TeleportDist       超出此距离时强制传送回玩家     ║
    // ╠══════════════════════════════════════════════════════╣
    // ║  【头节点 - 攻击】                                   ║
    // ║    SearchRange        索敌范围（玩家中心，50格）     ║
    // ║    BreakRange         脱战距离（玩家中心，70格）     ║
    // ║    AttackBaseAccel    基础角速度（弧度/帧）          ║
    // ║    AttackAccelLerp    加速度自身的平滑系数           ║
    // ║    AttackMinSpeed     攻击时最小速度                 ║
    // ║    AttackMaxSpeed     攻击时最大速度                 ║
    // ║    AttackSwerveDist   远距离开始绕弧的阈值           ║
    // ║    AttackSwerveRadius 绕弧偏移半径                   ║
    // ║    StuckTimeoutFrames 卡死保险阈值                   ║
    // ║    EngageDist         小于此距离时不再加速           ║
    // ╚══════════════════════════════════════════════════════╝

    public class DragonSegment : ModProjectile
    {
        // ── 通用 ──
        // 所有装备调用时可直接引用这两个常量，也可在装备侧重复定义数值：
        // PhantasmalDragonSummoner.MaintainFor(player, DragonSegment.SegmentDamage, DragonSegment.SegmentKnockback);
        public const int   SegmentDamage    = 123;
        public const float SegmentKnockback = 2f;
        public const int   HitCooldown      = 20;

        // ── 贴图 ──
        // 碰撞体积
        public const int FrameWidth   = 70;   
        public const int FrameHeight  = 70;   
        public const int HeadRow      = 0;
        public const int BodyRow      = 1;
        public const int TailRow      = 2;

        // ── 链结构 ──
        public const float SegmentDist     = 69f;
        public const float RotationDamping = 0.15f;

        // ── Idle 悬浮 ──
        public const float IdleOffsetX     = 60f;
        public const float IdleOffsetY     = -90f;
        public const float HoverAccelFar   = 0.2f;
        public const float HoverAccelMid   = 0.12f;
        public const float HoverAccelNear  = 0.06f;
        public const float HoverDamping    = 0.96f;
        public const float TeleportDist    = 16f * 110;

        // ── 攻击 ──
        public const float SearchRange        = 16f * 70;
        public const float BreakRange         = 16f * 100;
        public const float AttackBaseAccel    = 0.18f;
        public const float AttackAccelLerp    = 0.3f;
        // 追击速度
        public const float AttackMinSpeed     = 22f;
        public const float AttackMaxSpeed     = 42f;
        public const float AttackSwerveDist   = 400f;
        public const float AttackSwerveRadius = 145f;
        public const int   StuckTimeoutFrames = 90;
        public const float EngageDist         = 320f;

        // ──────────────────────────────────────────────────────────
        //   Projectile.ai[0] : 前节点 whoAmI（头=-1）
        //   Projectile.ai[1] : 段索引 0=头 1=身 2=尾
        //
        //   头节点 localAI：
        //   localAI[0] : swerve 周期帧计数
        //   localAI[1] : 当前角速度（lerp 平滑）
        //   localAI[2] : 卡死计时器
        // ──────────────────────────────────────────────────────────

        private int   PrevWhoAmI   => (int)Projectile.ai[0];
        private int   SegmentIndex => (int)Projectile.ai[1];
        private bool  IsHead       => SegmentIndex == 0;
        private bool  IsTail       => SegmentIndex == 2;

        public override string Texture => "TestMod/Projectiles/Minions/DragonSegment";

        public override void SetStaticDefaults()
        {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type]      = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width        = FrameWidth;
            Projectile.height       = FrameHeight;
            Projectile.friendly     = true;
            Projectile.hostile      = false;
            Projectile.minion       = true;
            Projectile.DamageType   = DamageClass.Generic;
            Projectile.penetrate    = -1;
            Projectile.tileCollide  = false;
            Projectile.ignoreWater  = true;
            Projectile.timeLeft     = 2;
            Projectile.minionSlots  = 0f;
            Projectile.aiStyle      = -1;
            Projectile.netImportant = true;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown  = HitCooldown;
        }

        // ══════════════════════════════════════════════════════════════
        //   AI 主入口
        //   头节点是大脑：跑自身飞行 + 驱动从属节点 SegmentMove
        //   非头节点 AI 几乎为空，移动由头驱动
        // ══════════════════════════════════════════════════════════════
        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            if (IsHead)
                HeadAI(owner);
            // 非头节点：等待头节点调用 SegmentMove()，timeLeft 由 Summoner 刷新
        }

        // ══════════════════════════════════════════════════════════════
        //   头节点 AI
        // ══════════════════════════════════════════════════════════════
        private void HeadAI(Player owner)
        {
            // 防丢失：太远直接传送
            Vector2 idealPos = owner.Center + new Vector2(IdleOffsetX, IdleOffsetY);
            float distFromIdeal = Vector2.Distance(Projectile.Center, idealPos);
            if (distFromIdeal > TeleportDist)
            {
                Projectile.Center    = owner.Center;
                Projectile.netUpdate = true;
            }

            // swerve 周期计数器
            Projectile.localAI[0] += 1f;

            // 索敌
            int targetIdx = FindTarget(owner.Center, SearchRange);

            if (targetIdx >= 0
                && Vector2.Distance(Projectile.Center, owner.Center) <= BreakRange)
            {
                AttackTarget(Main.npc[targetIdx]);
            }
            else
            {
                IdleHover(owner, distFromIdeal, idealPos);
                Projectile.localAI[2] = 0f;
            }

            // 旋转跟随实际速度
            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.spriteDirection = Projectile.velocity.X >= 0 ? 1 : -1;

            // 驱动所有从属节点
            DriveFollowers();
        }

        // ── Idle：玩家右上方悬停（加速度型，不画圆）──
        private void IdleHover(Player owner, float distFromIdeal, Vector2 idealPos)
        {
            float accel;
            if (distFromIdeal < 140f)      accel = HoverAccelNear;
            else if (distFromIdeal < 200f) accel = HoverAccelMid;
            else                           accel = HoverAccelFar;

            if (distFromIdeal > 100f)
            {
                if (Math.Abs(idealPos.X - Projectile.Center.X) > 20f)
                    Projectile.velocity.X += accel * Math.Sign(idealPos.X - Projectile.Center.X);
                if (Math.Abs(idealPos.Y - Projectile.Center.Y) > 10f)
                    Projectile.velocity.Y += accel * Math.Sign(idealPos.Y - Projectile.Center.Y);
            }
            else if (Projectile.velocity.Length() > 1f)
            {
                Projectile.velocity *= HoverDamping;
            }

            // 轻微反重力，避免下沉感
            if (Math.Abs(Projectile.velocity.Y) < 1f)
                Projectile.velocity.Y -= 0.1f;

            if (Projectile.velocity.Length() > AttackMaxSpeed)
                Projectile.velocity = Vector2.Normalize(Projectile.velocity) * AttackMaxSpeed;
        }

        // ── 攻击：远距离 swerve、视线对齐调速、角速度跟踪、卡死保险 ──
        private void AttackTarget(NPC target)
        {
            float idealAccel = AttackBaseAccel;
            Vector2 destination = target.Center;
            float distToDest = Vector2.Distance(Projectile.Center, destination);

            // 远距离：给目标位置加绕圈偏移，让接近成弧线
            if (distToDest > AttackSwerveDist)
            {
                Projectile.localAI[2] = 0f;
                float swerveAngle = Projectile.localAI[0] % 30f / 30f * MathHelper.TwoPi;
                destination += swerveAngle.ToRotationVector2() * AttackSwerveRadius;
                distToDest = Vector2.Distance(Projectile.Center, destination);
                idealAccel *= 2.5f;
            }

            // 加速度自身平滑（让加速度有惯性）
            Projectile.localAI[1] = MathHelper.Lerp(Projectile.localAI[1], idealAccel, AttackAccelLerp);

            // 视线对齐度
            Vector2 dirToTarget = (destination - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velDir      = Projectile.velocity.SafeNormalize(Vector2.Zero);
            float orth          = Vector2.Dot(velDir, dirToTarget);

            if (distToDest > EngageDist)
            {
                float speed = Projectile.velocity.Length();

                // 速度漂移
                if (speed < AttackMinSpeed + 7f) speed += 0.08f;
                if (speed > AttackMaxSpeed + 2f) speed -= 0.08f;

                // 接近对齐：加速猛冲
                if (orth < 0.85f && orth > 0.5f)  speed += 16f;
                // 偏离严重：减速调头
                if (orth < 0.5f && orth > -0.7f)  speed -= 16f;

                speed = MathHelper.Clamp(speed, AttackMinSpeed, AttackMaxSpeed + 4f);

                // 受限角速度逐步转向目标
                float curRot = Projectile.velocity.ToRotation();
                float toRot  = (destination - Projectile.Center).ToRotation();
                float newRot = curRot.AngleTowards(toRot, Projectile.localAI[1]);
                Projectile.velocity = newRot.ToRotationVector2() * speed;

                // 卡死保险
                Projectile.localAI[2] += 1f;
                if (Projectile.localAI[2] >= StuckTimeoutFrames)
                {
                    Projectile.velocity = (destination - Projectile.Center).SafeNormalize(Vector2.Zero) * (AttackMaxSpeed + 4f);
                    Projectile.localAI[2] = 0f;
                }
            }
            else
            {
                Projectile.localAI[2] = 0f;
            }
        }

        // ── 索敌 ──
        private int FindTarget(Vector2 ownerCenter, float range)
        {
            int best = -1;
            float bestDistSq = range * range;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                if (!npc.CanBeChasedBy()) continue;

                float dSq = Vector2.DistanceSquared(ownerCenter, npc.Center);
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = i;
                }
            }
            return best;
        }

        // ══════════════════════════════════════════════════════════════
        //   头节点：收集从属节点并按段索引顺序驱动它们
        //   1=连接1, 2=身, 3=连接2, 4=尾
        // ══════════════════════════════════════════════════════════════
        private void DriveFollowers()
        {
            DragonSegment[] followers = new DragonSegment[3];

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner) continue;
                if (p.type != Projectile.type) continue;
                if (p.whoAmI == Projectile.whoAmI) continue;

                int idx = (int)p.ai[1];
                if (idx >= 1 && idx <= 2 && followers[idx] == null)
                    followers[idx] = (DragonSegment)p.ModProjectile;
            }

            for (int idx = 1; idx <= 2; idx++)
            {
                DragonSegment seg = followers[idx];
                if (seg == null) continue;

                Projectile prev;
                if (idx == 1)
                {
                    prev = Projectile;  // 身体跟随头
                }
                else
                {
                    DragonSegment prevSeg = followers[idx - 1];
                    if (prevSeg == null) continue;
                    prev = prevSeg.Projectile;
                }

                // 身体用 velocity 前瞻让跟随更紧贴；尾巴不用，避免超前
                seg.SegmentMove(prev, useVelocityLookahead: !seg.IsTail);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   非头节点：跟随移动（仿黑龙 SegmentMove 算法）
        // ══════════════════════════════════════════════════════════════
        public void SegmentMove(Projectile prev, bool useVelocityLookahead)
        {
            Vector2 anchor = prev.Center + (useVelocityLookahead ? prev.velocity : Vector2.Zero);
            Vector2 destinationOffset = anchor - Projectile.Center;

            // 旋转阻尼：本节点旋转向前节点旋转平滑过渡
            if (prev.rotation != Projectile.rotation)
            {
                float angle = MathHelper.WrapAngle(prev.rotation - Projectile.rotation);
                destinationOffset = destinationOffset.RotatedBy(angle * RotationDamping);
            }

            if (destinationOffset != Vector2.Zero)
            {
                Projectile.rotation = destinationOffset.ToRotation();
                Projectile.Center   = anchor - destinationOffset.SafeNormalize(Vector2.Zero) * SegmentDist;
            }

            Projectile.velocity = Vector2.Zero;

            Projectile.spriteDirection = destinationOffset.X >= 0 ? 1 : -1;
        }

        // ══════════════════════════════════════════════════════════════
        //   PreDraw — 段0=头(行0) / 段1,2,3=身体(行1) / 段4=尾(行2)
        // ══════════════════════════════════════════════════════════════
        public override bool MinionContactDamage() => true;

        public override bool PreDraw(ref Color lightColor)
        {
            int row;
            if (IsHead)      row = HeadRow;
            else if (IsTail) row = TailRow;
            else             row = BodyRow;

            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;

            Rectangle srcRect = new Rectangle(
                0,
                row * FrameHeight,
                FrameWidth,
                FrameHeight
            );

            Vector2 origin = new Vector2(FrameWidth / 2f, FrameHeight / 2f);

            // rotation 已经是速度方向角，直接用即可表达正确朝向
            Main.EntitySpriteDraw(
                tex,
                Projectile.Center - Main.screenPosition,
                srcRect,
                lightColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );
            return false;
        }
    }
}
