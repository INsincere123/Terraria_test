using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using 武器test.Buffs;
using 武器test.Common.Players;

namespace 武器test.Projectiles.Minions
{
    /// <summary>
    /// 雷之呼吸·壹式召唤杖 的仆从 ——「雷之剑士」
    ///
    /// 核心机制:
    ///   【状态 0  FOLLOW 】 跟随玩家、靠近敌人时蓄力
    ///   【状态 1  DASH   】 任一仆从满蓄力时, 触发全体同步"一闪"冲刺
    ///   【状态 2  RECOVER】 冲刺后的短暂恢复, 然后回到跟随
    ///
    /// AI 字段分配(所有 ai[] 会自动网络同步):
    ///   ai[0] = 状态
    ///   ai[1] = 状态计时器
    ///   ai[2] = 冲刺目标 whoAmI
    ///   localAI[0] = 蓄力值(0~100, 本地累积, 通过 SendExtraAI 同步)
    /// </summary>
    public class ZenitsuMinion : ModProjectile
    {
        // ==================== 常量参数 ====================
        private const int STATE_FOLLOW = 0;
        private const int STATE_DASH = 1;
        private const int STATE_RECOVER = 2;

        private const float MAX_CHARGE = 100f;         // 满蓄力阈值
        private const float CHARGE_RATE_NEAR = 1.0f;   // 靠近敌人时蓄力速率
        private const float CHARGE_RATE_IDLE = 0.25f;  // 无敌人/远距离时慢速蓄力
        private const float OVERCHARGE_CAP = 150f;     // 过度蓄力上限(可选扩展)

        private const float DETECT_RANGE = 1200f;       // 敌人探测范围
        private const float CHARGE_RANGE = 600f;       // "靠近敌人"阈值

        private const int DASH_DURATION = 24;          // 冲刺持续帧数
        private const int RECOVER_DURATION = 30;       // 恢复持续帧数
        private const float DASH_SPEED = 32f;          // 冲刺速度(像素/帧)
        private const int MULTI_HIT_CD = 2;            // 冲刺多段命中间隔(帧)

        // ==================== 便捷属性 ====================
        public Player Owner => Main.player[Projectile.owner];
        public ref float State => ref Projectile.ai[0];
        public ref float StateTimer => ref Projectile.ai[1];
        public ref float DashTargetWhoAmI => ref Projectile.ai[2];
        public ref float Charge => ref Projectile.localAI[0];

        // ==================== 基础设置 ====================
        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 1;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 42;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.netImportant = true;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;

            // 多段伤害的关键: 开启 localNPCImmunity
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = MULTI_HIT_CD;
        }

        // ==================== 主 AI ====================
        public override void AI()
        {
            CheckActive();                              // 存活检查
            NPC target = FindTarget();                  // 统一寻敌

            switch ((int)State)
            {
                case STATE_FOLLOW:
                    FollowBehavior(target);
                    ChargeUp(target);
                    break;

                case STATE_DASH:
                    DashBehavior();
                    break;

                case STATE_RECOVER:
                    RecoverBehavior();
                    break;
            }

            UpdateVisuals();
        }

        // ==================== 存活维持 ====================
        private void CheckActive()
        {
            var modPlayer = Owner.GetModPlayer<ThunderBreathingMinionPlayer>();

            // 每帧向游戏声明"我还在"，Buff 系统会自动维持
            // 注意：不在这里设置 thunderBreathing = true
            // 该标志只由 ThunderBreathingBuff.Update 负责设置
            // 这样右键取消 Buff 后，Buff.Update 停止运行 → 标志保持 false → 仆从自然消亡
            Owner.AddBuff(ModContent.BuffType<ThunderBreathingBuff>(), 2);

            // 玩家死亡时强制清除标志，确保 Buff 能自行删除
            if (Owner.dead || !Owner.active)
            {
                modPlayer.thunderBreathing = false;
                return;
            }

            // 标志由 Buff.Update 设置；右键取消后标志不再为 true，仆从寿命不再刷新
            if (modPlayer.thunderBreathing)
                Projectile.timeLeft = 2;
        }

        // ==================== 寻敌 ====================
        private NPC FindTarget()
        {
            // 优先: 玩家右键标记的目标
            if (Owner.HasMinionAttackTargetNPC)
            {
                NPC t = Main.npc[Owner.MinionAttackTargetNPC];
                if (t.CanBeChasedBy(Projectile) && Projectile.Distance(t.Center) < DETECT_RANGE * 2f)
                    return t;
            }

            // 否则寻找范围内最近的敌人
            NPC best = null;
            float closest = DETECT_RANGE;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(Projectile))
                    continue;
                float d = Projectile.Distance(npc.Center);
                if (d < closest)
                {
                    closest = d;
                    best = npc;
                }
            }
            return best;
        }

        // ==================== 状态 0: 跟随 ====================
        private void FollowBehavior(NPC target)
        {
            // 给每个同类仆从分配一个环绕位置(防止重叠)
            int myIndex = 0, totalCount = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == Type)
                {
                    if (p.whoAmI == Projectile.whoAmI) myIndex = totalCount;
                    totalCount++;
                }
            }

            // 默认环绕玩家的待命位置
            float angle = MathHelper.TwoPi * myIndex / Math.Max(totalCount, 1)
                          + Main.GameUpdateCount * 0.02f;
            Vector2 idleOffset = new Vector2(
                (float)Math.Cos(angle) * 90f,
                (float)Math.Sin(angle) * 40f - 70f);
            Vector2 desiredPos = Owner.Center + idleOffset;

            // 有目标时: 略微向目标方向偏移(保持骚扰感, 不直接冲进敌人)
            if (target != null && Projectile.Distance(target.Center) < CHARGE_RANGE)
            {
                Vector2 harassPos = target.Center
                    + (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitY) * 160f;
                desiredPos = Vector2.Lerp(desiredPos, harassPos, 0.35f);
            }

            // 平滑向期望位置移动
            Vector2 toDesired = desiredPos - Projectile.Center;
            float dist = toDesired.Length();
            if (dist > 10f)
            {
                float moveSpeed = MathHelper.Clamp(dist * 0.1f, 4f, 14f);
                Projectile.velocity = (Projectile.velocity * 14f
                    + toDesired.SafeNormalize(Vector2.Zero) * moveSpeed) / 15f;
            }
            else
            {
                Projectile.velocity *= 0.85f;
            }

            // 面朝
            Projectile.spriteDirection = Projectile.direction =
                (target != null ? target.Center.X > Projectile.Center.X
                                : Projectile.velocity.X > 0) ? 1 : -1;
            Projectile.rotation = Projectile.velocity.X * 0.02f;
        }

        // ==================== 蓄力 ====================
        private void ChargeUp(NPC target)
        {
            if (target != null)
            {
                // 靠近敌人蓄力更快；远距离慢速蓄力(避免完全停滞)
                float rate = Projectile.Distance(target.Center) < CHARGE_RANGE
                    ? CHARGE_RATE_NEAR
                    : CHARGE_RATE_IDLE;
                Charge = Math.Min(Charge + rate, OVERCHARGE_CAP);
            }
            else
            {
                // 无敌人时蓄力缓慢衰减(防止野外蓄满空挥)
                Charge = Math.Max(Charge - 0.1f, 0f);
            }

            // 满蓄力触发全体同步冲刺 —— 仅本地玩家执行避免重复触发
            if (Charge >= MAX_CHARGE && target != null
                && Projectile.owner == Main.myPlayer)
            {
                // 距离验证: 防止目标飘远空挥(可选扩展)
                if (Projectile.Distance(target.Center) < CHARGE_RANGE * 1.2f)
                    TriggerGlobalDash(target);
            }
        }

        // ==================== 全体同步冲刺 ====================
        private void TriggerGlobalDash(NPC target)
        {
            bool triggeredAny = false;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner || p.type != Type)
                    continue;
                if ((int)p.ai[0] != STATE_FOLLOW)
                    continue;

                // 设状态与目标
                p.ai[0] = STATE_DASH;
                p.ai[1] = 0;
                p.ai[2] = target.whoAmI;
                p.localAI[0] = 0f;

                // 朝目标方向设置初始速度(每个仆从独立算方向)
                Vector2 dashDir = (target.Center - p.Center).SafeNormalize(Vector2.UnitX);
                p.velocity = dashDir * DASH_SPEED;

                // 重置局部命中CD, 确保新一轮冲刺可以重新造成伤害
                for (int k = 0; k < p.localNPCImmunity.Length; k++)
                    p.localNPCImmunity[k] = 0;

                p.netUpdate = true;
                triggeredAny = true;
            }

            if (triggeredAny)
            {
                // 集体爆发声效 —— 一个清脆的雷击音
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f, Volume = 0.9f },
                    Projectile.Center);

                // 整个玩家周围亮一下
                for (int i = 0; i < 20; i++)
                {
                    Dust d = Dust.NewDustPerfect(Owner.Center,
                        DustID.Electric,
                        Main.rand.NextVector2Circular(8f, 8f),
                        0, default, 1.5f);
                    d.noGravity = true;
                }
            }
        }

        // ==================== 状态 1: 冲刺"一闪" ====================
        private void DashBehavior()
        {
            StateTimer++;

            int targetIdx = (int)DashTargetWhoAmI;
            NPC target = (targetIdx >= 0 && targetIdx < Main.maxNPCs)
                ? Main.npc[targetIdx] : null;

            // 前 6 帧可微调方向, 之后直线冲刺("一闪"的直线感)
            if (target != null && target.active && !target.friendly && StateTimer < 6)
            {
                Vector2 dashDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dashDir * DASH_SPEED, 0.3f);
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.spriteDirection = Projectile.direction = Projectile.velocity.X > 0 ? 1 : -1;

            // 雷电粒子
            Dust lightning = Dust.NewDustPerfect(Projectile.Center,
                DustID.Electric, -Projectile.velocity * 0.25f, 0, default, 1.4f);
            lightning.noGravity = true;

            Lighting.AddLight(Projectile.Center, 0.8f, 0.8f, 0.3f);

            // 冲刺结束
            if (StateTimer >= DASH_DURATION)
            {
                State = STATE_RECOVER;
                StateTimer = 0;
                Projectile.netUpdate = true;
            }
        }

        // ==================== 状态 2: 恢复 ====================
        private void RecoverBehavior()
        {
            StateTimer++;

            // 减速
            Projectile.velocity *= 0.9f;

            // 若离玩家过远则轻微向玩家靠近
            Vector2 toOwner = Owner.Center - Projectile.Center;
            if (toOwner.Length() > 400f)
                Projectile.velocity += toOwner.SafeNormalize(Vector2.Zero) * 0.5f;

            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, 0f, 0.15f);

            if (StateTimer >= RECOVER_DURATION)
            {
                State = STATE_FOLLOW;
                StateTimer = 0;
                Charge = 0f;
                Projectile.netUpdate = true;
            }
        }

        // ==================== 视觉效果 ====================
        private void UpdateVisuals()
        {
            // 蓄力 >= 80% 时闪烁电火花, 给玩家"即将爆发"的预警
            if (Charge >= MAX_CHARGE * 0.8f && (int)State == STATE_FOLLOW)
            {
                if (Main.rand.NextBool(3))
                {
                    Dust spark = Dust.NewDustPerfect(
                        Projectile.Center + Main.rand.NextVector2Circular(16f, 20f),
                        DustID.Electric, Vector2.Zero, 0, default, 1.0f);
                    spark.noGravity = true;
                    spark.velocity *= 0.5f;
                }
                Lighting.AddLight(Projectile.Center, 0.4f, 0.4f, 0.1f);
            }
        }

        // ==================== 伤害控制 ====================
        // 仅在冲刺状态产生接触伤害, 跟随状态不伤人(实现"节奏型"输出结构)
        public override bool? CanDamage() => (int)State == STATE_DASH;
        public override bool MinionContactDamage() => (int)State == STATE_DASH;

        // ==================== 护甲穿透 ====================
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // 冲刺命中时无视 20 点防御（「一闪」穿甲效果）
            if ((int)State == STATE_DASH)
                modifiers.ArmorPenetration += 20;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 击中目标的雷电粒子反馈
            for (int i = 0; i < 8; i++)
            {
                Dust d = Dust.NewDustPerfect(target.Center,
                    DustID.Electric,
                    Main.rand.NextVector2Circular(6f, 6f),
                    0, default, 1.3f);
                d.noGravity = true;
            }

            // 仅本地玩家发射，避免联机重复生成
            if (Projectile.owner != Main.myPlayer)
                return;

            // 沿当前冲刺速度方向发射 Electrosphere
            // velocity 非零时用冲刺方向；否则用仆从 → 目标方向
            Vector2 slashDir = Projectile.velocity.LengthSquared() > 0.01f
                ? Projectile.velocity.SafeNormalize(Vector2.UnitX)
                : (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);

            // 在目标中心偏后一点生成，模拟"穿过之后的刀气"
            Vector2 spawnPos = target.Center - slashDir * 120f;

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPos,
                slashDir * 8f,                  // 刀气速度(较慢，视觉停留)
                ProjectileID.Electrosphere,
                Projectile.damage,              // 继承仆从伤害
                0f,                             // 击退(仆从刀气通常不额外击退)
                Projectile.owner
            );
        }

        // ==================== 网络同步 ====================
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Charge);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Charge = reader.ReadSingle();
        }

        // ==================== 绘制 ====================
        public override bool PreDraw(ref Color lightColor)
        {
            // 冲刺时绘制黄色残影
            if ((int)State == STATE_DASH)
            {
                Texture2D tex = TextureAssets.Projectile[Type].Value;
                Vector2 origin = tex.Size() / 2;
                SpriteEffects effects = Projectile.spriteDirection == -1
                    ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    float alpha = 1f - i / (float)Projectile.oldPos.Length;
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2 - Main.screenPosition;
                    Color trailColor = Color.Yellow * alpha * 0.6f;
                    Main.EntitySpriteDraw(tex, drawPos, null, trailColor,
                        Projectile.rotation, origin, Projectile.scale, effects, 0);
                }
            }
            return true;
        }
    }
}
