using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;
using ProjectileTracking = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Content.Projectiles.Minions
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
    // ║    运动参数见 DragonFlightSettings.Phantom          ║
    // ║    攻击分为追击、穿行和回转；速度与方向分开平滑     ║
    // ╚══════════════════════════════════════════════════════╝

    public class DragonSegment : ModProjectile
    {
        // ── 通用 ──
        // 所有装备调用时可直接引用这两个常量，也可在装备侧重复定义数值：
        // PhantasmalDragonSummoner.MaintainFor(player, DragonSegment.SegmentDamage, DragonSegment.SegmentKnockback);
        public const int   SegmentDamage    = 123;
        public const float SegmentKnockback = 2f;
        public const int   HitCooldown      = 15;

        // ── 贴图 ──
        // 碰撞体积
        public const int FrameWidth   = 70;   
        public const int FrameHeight  = 70;   
        public const int HeadRow      = 0;
        public const int BodyRow      = 1;
        public const int TailRow      = 2;

        // ── 链结构 ──
        public const float SegmentDist     = 25.5f;
        public const float VisualOverlapDistance = 25f;
        public const float RotationDamping = 0.16f;
        public const float FollowLerp = 0.86f;

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
        // 旧公开常量保留源级兼容；新的攻击运动读取 DragonFlightSettings.Phantom。
        // AttackMaxSpeed 仍用于原闲置悬浮限速。
        public const float AttackBaseAccel    = 0.22f;
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
        //   Projectile.ai[1] : 段索引 0=头，1..11=身，12=尾
        //   攻击阶段保存在每头独立的 _flight 中，通过 ExtraAI 同步。
        // ──────────────────────────────────────────────────────────

        public const int HeadSegmentKind = 0;
        public const int BodySegmentKind = 1;
        public const int TailSegmentKind = 2;

        private int PrevWhoAmI => (int)Projectile.ai[0];
        private ProjectileTargetCache _targetCache;
        private DragonFlightController _flight;
        internal int SegmentIndex => (int)Projectile.ai[1];
        internal int SegmentKind
        {
            get
            {
                if (SegmentIndex <= 0)
                    return HeadSegmentKind;

                if (SegmentIndex >= PhantasmalDragonSummoner.SegmentCount - 1)
                    return TailSegmentKind;

                return BodySegmentKind;
            }
        }

        internal bool IsHeadSegment => SegmentKind == HeadSegmentKind;
        internal bool IsTailSegment => SegmentKind == TailSegmentKind;
        internal bool DealsContactDamage => IsHeadSegment || (SegmentKind == BodySegmentKind && SegmentIndex % 4 == 0);

        public override string Texture => "TestMod/Content/Projectiles/Minions/DragonSegment";

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
                _flight.Reset();
                Projectile.Kill();
                return;
            }

            if (IsHeadSegment)
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
                ProjectileTracking.MarkTrackingActivity(Projectile, false, _flight.Reset());
                _targetCache = default;
                distFromIdeal = Vector2.Distance(Projectile.Center, idealPos);
            }

            // owner 选择目标，远端使用收到的目标预测，不独立重选另一只敌人。
            bool authority = Projectile.owner == Main.myPlayer;
            bool changed = false;
            bool canAttack = Vector2.DistanceSquared(Projectile.Center, owner.Center) <= BreakRange * BreakRange;
            if (authority && canAttack)
            {
                int targetIdx = _targetCache.FindNearest(Projectile, owner.Center, SearchRange, out bool replaced);
                changed = targetIdx >= 0 ? _flight.SetTarget(targetIdx, Main.npc[targetIdx].type, replaced) : _flight.Reset();
            }
            NPC target = canAttack ? _flight.ResolveTarget(Projectile, owner.Center, SearchRange) : null;
            if (target != null)
            {
                changed |= AttackTarget(target, authority);
            }
            else
            {
                changed |= _flight.Reset();
                IdleHover(owner, distFromIdeal, idealPos);
            }
            ProjectileTracking.MarkTrackingActivity(Projectile, target != null, changed);

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

        // ── 攻击：近距离仍连续转向，穿行后让整条龙沿弧线回转 ──
        private bool AttackTarget(NPC target, bool authority)
        {
            Projectile.velocity = _flight.Update(Projectile.Center, Projectile.Size, target.Hitbox,
                target.velocity, Projectile.velocity, SegmentDist * (PhantasmalDragonSummoner.SegmentCount - 1),
                1f / Projectile.MaxUpdates, DragonFlightSettings.Phantom, authority, false, out bool changed);
            return changed;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            if (IsHeadSegment) _flight.Write(writer);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            if (IsHeadSegment) _flight.Read(reader);
        }

        // ══════════════════════════════════════════════════════════════
        //   头节点：收集从属节点并按段索引顺序驱动它们
        //   1=连接1, 2=身, 3=连接2, 4=尾
        // ══════════════════════════════════════════════════════════════
        private void DriveFollowers()
        {
            Projectile[] segments = PhantasmalDragonSummoner.GetSegments(Projectile.owner);
            segments[0] = Projectile;

            for (int idx = 1; idx < segments.Length; idx++)
            {
                if (segments[idx]?.ModProjectile is not DragonSegment seg) continue;

                Projectile prev;
                if (idx == 1)
                {
                    prev = Projectile;  // 身体跟随头
                }
                else
                {
                    prev = segments[idx - 1];
                    if (prev == null) continue;
                }

                // 身体用 velocity 前瞻让跟随更紧贴；尾巴不用，避免超前
                seg.SegmentMove(prev, useVelocityLookahead: !seg.IsTailSegment);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   非头节点：跟随移动（仿黑龙 SegmentMove 算法）
        // ══════════════════════════════════════════════════════════════
        public void SegmentMove(Projectile prev, bool useVelocityLookahead)
        {
            Vector2 anchor = prev.Center + (useVelocityLookahead ? prev.velocity * 0.45f : Vector2.Zero);
            Vector2 destinationOffset = anchor - Projectile.Center;

            // 旋转阻尼：本节点旋转向前节点旋转平滑过渡
            if (prev.rotation != Projectile.rotation)
            {
                float angle = MathHelper.WrapAngle(prev.rotation - Projectile.rotation);
                destinationOffset = destinationOffset.RotatedBy(angle * RotationDamping);
            }

            if (destinationOffset != Vector2.Zero)
            {
                float targetRotation = destinationOffset.ToRotation();
                Projectile.rotation = Projectile.rotation.AngleTowards(targetRotation, 0.42f);

                Vector2 direction = Projectile.rotation.ToRotationVector2();
                Vector2 targetCenter = anchor - direction * SegmentDist;
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetCenter, FollowLerp);
            }

            Projectile.velocity = Vector2.Zero;

            Projectile.spriteDirection = destinationOffset.X >= 0 ? 1 : -1;
        }

        // ══════════════════════════════════════════════════════════════
        //   PreDraw — 段0=头(行0) / 段1,2,3=身体(行1) / 段4=尾(行2)
        // ══════════════════════════════════════════════════════════════
        public override bool MinionContactDamage() => DealsContactDamage;

        internal bool TryGetPreviousSegment(out Projectile previous)
        {
            previous = null;

            if (PrevWhoAmI < 0 || PrevWhoAmI >= Main.maxProjectiles)
                return false;

            Projectile candidate = Main.projectile[PrevWhoAmI];
            if (!candidate.active || candidate.owner != Projectile.owner || candidate.type != Projectile.type)
                return false;

            previous = candidate;
            return true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (IsHeadSegment)
                DrawUtils.DrawPhantasmalDragonChain(this, lightColor);

            return false;
        }
    }
}
