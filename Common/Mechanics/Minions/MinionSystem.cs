using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TestMod.Common.Utilities;

namespace TestMod.Common.Mechanics.Minions
{
    /// <summary>
    /// 召唤物增强系统。
    ///
    /// 职责：分类判定 + 追踪行为 + 独立无敌帧 + 命中弹开。
    /// GlobalProjectile 的 SetDefaults/PostAI/OnHitNPC 通过薄包装层调用本类，
    /// 本类不直接持有任何 GlobalProjectile 状态。
    ///
    /// 召唤物分为三类：
    ///   ① 冲撞型（ContactMinion）    — 靠身体撞击，应用扩范围追踪 + 弹开
    ///   ② 射击型本体（ShootingBody） — 保持距离发射子弹，不干预
    ///   ③ 召唤物射弹（MinionShot）   — 由 GlobalProjectile 通用追踪处理
    ///   专属处理                      — 星尘龙/乌鸦/沙漠虎/蜘蛛/泰拉棱镜
    /// </summary>
    public static class MinionSystem
    {
        // ── 数值调节区：冲撞型召唤物 ────────────────────────────────
        public const float ContactTargetRange = 3200f;  // 瞄准距离（200 图格）
        public const float ContactReturnDist  = 5600f;  // 超出此距离传送回玩家
        public const int   ContactHitCooldown = 20;     // 独立无敌帧（≈0.33s）
        public const float ContactChaseSpeed  = 21f;    // 追击速度
        public const float ContactBounceSpeed = 12f;    // 命中后弹开速度
        // ─────────────────────────────────────────────────────────────

        // ── 数值调节区：蜘蛛法杖 ────────────────────────────────────
        public const float SpiderTargetRange  = 3200f;  // 扩展后瞄准距离
        public const float SpiderReturnDist   = 5600f;  // 扩展后返回阈值（4× 原版）
        public const float SpiderVanillaReturn = 1400f; // 原版返回阈值
        public const int   SpiderHitCooldown  = 20;     // 20 帧（匹配原版 0.33s 内部冷却）
        // ─────────────────────────────────────────────────────────────

        // ══════════════════════════════════════════════════════════════
        //   ② 射击型本体列表（保持距离发射射弹的召唤物本体）
        //
        //   判定标准：vanilla AI 会在一定距离外停下来发射射弹。
        //   加入后效果：跳过所有追踪强化，完全保持 vanilla AI。
        //   遇到"贴敌不开枪"症状 → 加入此表即可修复。
        // ══════════════════════════════════════════════════════════════
        public static readonly HashSet<int> ShootingMinionBodies = new()
        {
            ProjectileID.StardustCellMinion,   // 613 星尘细胞（发 StardustCellMinionShot 614）
            ProjectileID.FlyingImp,            //     小恶魔（发火球）
            ProjectileID.Retanimini,           // 387 迷你视网膜（发 MiniRetinaLaser 389）
            ProjectileID.Tempest,              // 407 风暴法杖本体（发 MiniSharkron 408）
            ProjectileID.Hornet,               //     大黄蜂（发 HornetStinger）
            ProjectileID.Pygmy,                // 191 矮人
            ProjectileID.Pygmy2,
            ProjectileID.Pygmy3,
            ProjectileID.Pygmy4,
        };

        // ══════════════════════════════════════════════════════════════
        //   完全保持 vanilla 的召唤物（不追踪、不弹开、不改无敌帧）
        // ══════════════════════════════════════════════════════════════
        public static readonly HashSet<int> VanillaMinionExclusions = new()
        {
            ProjectileID.Smolstar,             // 864 刃杖
            ProjectileID.UFOMinion,            // 423 UFO（发 UFOLaser 433）
            ProjectileID.OneEyedPirate,        //     海盗法杖
            ProjectileID.SoulscourgePirate,
        };

        // ══════════════════════════════════════════════════════════════
        //   分类判定
        // ══════════════════════════════════════════════════════════════

        /// <summary>蜘蛛法杖的三种蜘蛛 + 阿比盖尔之花。</summary>
        public static bool IsSpiderMinion(int type)
            => type == ProjectileID.VenomSpider
            || type == ProjectileID.JumperSpider
            || type == ProjectileID.DangerousSpider
            || type == ProjectileID.AbigailMinion;

        /// <summary>已有专属追踪逻辑的召唤物（不走通用分类）。</summary>
        public static bool IsSpeciallyHandledMinion(int type)
        {
            if (ProjectileID.Sets.StardustDragon[type]) return true;
            if (type == ProjectileID.Raven)             return true;
            if (type == ProjectileID.StormTigerTier1)  return true;
            if (type == ProjectileID.StormTigerTier2)  return true;
            if (type == ProjectileID.StormTigerTier3)  return true;
            if (type == ProjectileID.EmpressBlade)     return true;
            if (IsSpiderMinion(type))                  return true;
            return false;
        }

        /// <summary>② 射击型本体（保持距离发射射弹）。</summary>
        public static bool IsShootingMinionBody(int type)
            => ShootingMinionBodies.Contains(type);

        /// <summary>
        /// ① 冲撞型召唤物（vanilla 本体，排除 sentry / 模组 / 射击型 / 专属处理）。
        /// </summary>
        public static bool IsContactMinion(Projectile projectile)
        {
            if (VanillaMinionExclusions.Contains(projectile.type)) return false;
            if (!projectile.minion)                                 return false;
            if (projectile.sentry)                                  return false;
            if (projectile.type >= ProjectileID.Count)              return false; // 排除模组
            if (IsShootingMinionBody(projectile.type))              return false;
            if (IsSpeciallyHandledMinion(projectile.type))          return false;
            return true;
        }

        // ══════════════════════════════════════════════════════════════
        //   ① 冲撞型：SetDefaults 启用独立无敌帧
        // ══════════════════════════════════════════════════════════════
        public static void ApplyContactMinionSetDefaults(Projectile projectile)
        {
            if (VanillaMinionExclusions.Contains(projectile.type)) return;
            if (!projectile.minion)                                 return;
            if (projectile.sentry)                                  return;
            if (projectile.type >= ProjectileID.Count)              return;
            if (IsShootingMinionBody(projectile.type))              return;
            if (IsSpeciallyHandledMinion(projectile.type))          return;

            // 关闭同类型共享无敌，启用每实例独立无敌
            projectile.usesIDStaticNPCImmunity = false;
            projectile.usesLocalNPCImmunity    = true;
            projectile.localNPCHitCooldown     = ContactHitCooldown;
        }

        // ══════════════════════════════════════════════════════════════
        //   ① 冲撞型：PostAI 扩大范围追踪
        //
        //   策略：
        //     · > ContactReturnDist → 传送回玩家
        //     · ContactTargetRange 内有目标 → 覆写 velocity 追击
        //     · 其他 → 不干预，交给 vanilla 处理回归
        // ══════════════════════════════════════════════════════════════
        public static void ApplyContactMinionTracking(Projectile projectile, Player player)
        {
            float distToPlayer = Vector2.Distance(projectile.Center, player.Center);

            // ── 超出返回阈值：传送回玩家 ──
            if (distToPlayer > ContactReturnDist)
            {
                projectile.position = player.Center
                    + new Vector2(Main.rand.NextFloat(-80f, 80f), -60f)
                    - projectile.Size * 0.5f;
                projectile.velocity = Vector2.Zero;
                projectile.netUpdate = true;
                return;
            }

            // ── 扩展瞄准 ──
            int targetIdx = TargetUtils.FindNearest(projectile.Center, ContactTargetRange);
            if (targetIdx < 0) return; // 无目标，交给 vanilla 处理回归

            NPC target = Main.npc[targetIdx];
            Vector2 toTarget = target.Center - projectile.Center;
            float dist = toTarget.Length();
            if (dist < 60f) return; // 太近，让 vanilla 处理接触手感

            toTarget /= dist;
            projectile.velocity = Vector2.Lerp(
                projectile.velocity,
                toTarget * ContactChaseSpeed,
                0.15f);
        }

        // ══════════════════════════════════════════════════════════════
        //   ① 冲撞型：OnHitNPC 命中后弹开
        //
        //   防止贴在 boss 身上空转无敌帧，制造"打-退-进-打"节奏。
        //   蜘蛛命中时同样调用此方法。
        // ══════════════════════════════════════════════════════════════
        public static void ApplyContactMinionBounce(Projectile projectile, NPC target)
        {
            Vector2 away = projectile.Center - target.Center;
            if (away.LengthSquared() < 1f)
                away = -Vector2.UnitY; // 完全重叠时 fallback 向上弹
            else
                away.Normalize();

            projectile.velocity = away * ContactBounceSpeed;
            projectile.netUpdate = true;
        }

        // ══════════════════════════════════════════════════════════════
        //   蜘蛛：SetDefaults 启用独立无敌帧
        // ══════════════════════════════════════════════════════════════
        public static void ApplySpiderSetDefaults(Projectile projectile)
        {
            if (!IsSpiderMinion(projectile.type)) return;

            // 关闭同类型共享无敌（原版 15 帧），改为每实例独立
            projectile.usesIDStaticNPCImmunity = false;
            projectile.usesLocalNPCImmunity    = true;
            projectile.localNPCHitCooldown     = SpiderHitCooldown;
        }

        // ══════════════════════════════════════════════════════════════
        //   蜘蛛：PostAI 扩大瞄准 + 延长返回阈值
        //
        //   策略（不替换 vanilla AI，只覆写 velocity）：
        //     · > SpiderReturnDist → 传送回玩家
        //     · SpiderTargetRange 内有目标 → 覆写 velocity 追击
        //     · 无目标且 > SpiderVanillaReturn → 衰减速度，阻止 vanilla 拉回
        //     · 其他 → 不干预，vanilla 正常悬停
        // ══════════════════════════════════════════════════════════════
        public static void ApplySpiderPostAI(Projectile projectile, Player player)
        {
            if (!IsSpiderMinion(projectile.type)) return;

            float distToPlayer = Vector2.Distance(projectile.Center, player.Center);

            // ── 超出 4× 阈值：传送回玩家 ──
            if (distToPlayer > SpiderReturnDist)
            {
                projectile.position = player.Center
                    + new Vector2(Main.rand.NextFloat(-80f, 80f), -60f)
                    - projectile.Size * 0.5f;
                projectile.velocity = Vector2.Zero;
                projectile.netUpdate = true;
                return;
            }

            // ── 扩展瞄准 ──
            int targetIdx = TargetUtils.FindNearest(projectile.Center, SpiderTargetRange);

            if (targetIdx >= 0)
            {
                NPC target = Main.npc[targetIdx];
                Vector2 toTarget = target.Center - projectile.Center;
                float dist = toTarget.Length();

                if (dist > 60f)
                {
                    toTarget /= dist;
                    const float chaseSpeed = 21f;
                    projectile.velocity = Vector2.Lerp(
                        projectile.velocity,
                        toTarget * chaseSpeed,
                        0.15f);
                }
                // 近距离（<60px）交给 vanilla 处理接触伤害手感
            }
            else if (distToPlayer > SpiderVanillaReturn)
            {
                // 无目标但已超出原版返回阈值：阻止 vanilla 拉回，让蜘蛛在扩展区域悬停
                projectile.velocity *= 0.9f;
            }
        }
    }
}
