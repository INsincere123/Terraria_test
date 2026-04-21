using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace 武器test
{
    public partial class MyGlobalProjectile
    {
        // ═══════════════════════════════════════════════════════════════════
        //   召唤物分类系统
        //
        //   根据行为将 vanilla 召唤物分为三类,分别使用不同的强化策略。
        //   ──────────────────────────────────────────────────────────────
        //   ① 冲撞型 (Contact Minion)
        //     例: 小跟班/南瓜使者/独眼海盗/迷你视网膜/吸血蛙/致命球...
        //     特征: 靠身体撞击造成伤害
        //     处理: 蜘蛛式扩大追击范围 + 独立无敌帧 + 命中后弹开
        //   ──────────────────────────────────────────────────────────────
        //   ② 射击型本体 (Shooting Minion Body)
        //     例: 星尘细胞本体 (在一定距离外停下发射子细胞)
        //     特征: 保持距离发射射弹,贴敌反而不开枪
        //     处理: 完全保持 vanilla AI,不做任何干预
        //     (它们发射的射弹由 ③ 单独强化)
        //   ──────────────────────────────────────────────────────────────
        //   ③ 召唤物射弹 (MinionShot)
        //     例: 星尘细胞子弹/大黄蜂尖刺/UFO激光/双子迷你激光...
        //     特征: 从召唤物本体射出的攻击射弹
        //     处理: 通用高阶追踪 (godMode 开启时生效)
        //     判断: ProjectileID.Sets.MinionShot (vanilla 官方集合)
        //           当前含 {195, 374, 376, 389, 408, 433, 614, 818}
        //   ──────────────────────────────────────────────────────────────
        //
        //   【已有专属处理 ── 不走通用分类】
        //     · 星尘龙全系 (ProjectileID.Sets.StardustDragon)
        //     · 乌鸦      (PreAI 完全接管)
        //     · 沙漠虎三形态 (龙头级追踪)
        //     · 蜘蛛三形态  (MyGlobalProjectile.Spider.cs)
        //     · 泰拉棱镜   (特定高阶追踪参数)
        // ═══════════════════════════════════════════════════════════════════

        // ┌──────────────────────────────────────────────────────────────┐
        // │ ▼▼▼  ② 射击型本体列表  ── 用户可自行维护  ▼▼▼                │
        // │                                                              │
        // │ 【判定标准】                                                  │
        // │   vanilla AI 会在一定距离外停下来发射射弹的召唤物本体。        │
        // │                                                              │
        // │ 【加入后效果】                                                │
        // │   该召唤物本体会跳过所有追踪强化、独立无敌帧和弹开逻辑,       │
        // │   完全保持 vanilla AI,避免贴敌不开枪的问题。                  │
        // │                                                              │
        // │ 【注意】                                                      │
        // │   · 它们发射的射弹走 ③ 分支 (由 MinionShot 自动识别),         │
        // │     不受此列表影响。                                          │
        // │   · 如果某召唤物表现出"贴敌不开枪"的症状,加入此表即可修复。   │
        // └──────────────────────────────────────────────────────────────┘
        public static readonly HashSet<int> ShootingMinionBodies = new HashSet<int>
        {
            ProjectileID.StardustCellMinion,    // 613  星尘细胞 (发 StardustCellMinionShot 614)

            // 凡是 vanilla 召唤物本体（minion == true 且不是模组）——只要不在 ShootingMinionBodies 里，也不是专属处理的那几个（龙/乌鸦/虎/蜘蛛/棱镜）——就自动归入冲撞型。
            // 遇到某个召唤物贴敌不开枪 → 加进去
            // 不确定的先不加，实际测试看表现，有问题再加
            ProjectileID.FlyingImp,          //   小恶魔       (发火球)
            ProjectileID.Retanimini,         // 387  迷你视网膜   (发 MiniRetinaLaser 389)
            ProjectileID.Tempest,            // 407  风暴法杖本体 (发 MiniSharkron 408)
            ProjectileID.Hornet,             //   大黄蜂       (发 HornetStinger)
            ProjectileID.Pygmy,              // 191  矮人
            ProjectileID.Pygmy2,
            ProjectileID.Pygmy3,
            ProjectileID.Pygmy4,
        };

        // ┌──────────────────────────────────────────────────────────────┐
        // │ 完全保持 vanilla 的召唤物 ── 不应用任何强化                    │
        // │ 既不追踪、不弹开、也不改无敌帧                                 │
        // └──────────────────────────────────────────────────────────────┘
        public static readonly HashSet<int> VanillaMinionExclusions = new HashSet<int>
        {
            ProjectileID.Smolstar,   // 864  刃杖
            ProjectileID.UFOMinion,          // 423  UFO         (发 UFOLaser 433)
            ProjectileID.OneEyedPirate,          // 海盗法杖
            ProjectileID.SoulscourgePirate,
            //ProjectileID.PirateCaptain,
        };

        // ══════════════════════════════════════════════════════════════════
        //   ① 冲撞型参数 ── 与蜘蛛参数对齐
        // ══════════════════════════════════════════════════════════════════
        private const float CONTACT_MINION_TARGET_RANGE = 3200f;   // 瞄准距离 (200 图格)
        private const float CONTACT_MINION_RETURN_DIST = 5600f;   // 超出此距离传送回玩家
        private const int CONTACT_MINION_HIT_COOLDOWN = 20;      // 独立无敌帧 (≈0.33s)
        private const float CONTACT_MINION_CHASE_SPEED = 21f;     // 追击速度
        private const float CONTACT_MINION_BOUNCE_SPEED = 12f;     // 命中后弹开速度

        // ═══════════════════════════════════════════════════════════════════
        //   判定辅助
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 是否为有专属追踪逻辑的召唤物 (已被 PostAI 其他分支单独处理)。
        /// 这些召唤物既不应用通用分类,也不应用通用 SetDefaults 修改。
        /// </summary>
        public static bool IsSpeciallyHandledMinion(int type)
        {
            if (ProjectileID.Sets.StardustDragon[type]) return true;
            if (type == ProjectileID.Raven) return true;
            if (type == ProjectileID.StormTigerTier1) return true;
            if (type == ProjectileID.StormTigerTier2) return true;
            if (type == ProjectileID.StormTigerTier3) return true;
            if (type == ProjectileID.EmpressBlade) return true;
            if (IsSpiderMinion(type)) return true;
            return false;
        }

        /// <summary>
        /// 是否为 ② 射击型本体 (保持距离发射射弹的召唤物本体)
        /// </summary>
        public static bool IsShootingMinionBody(int type) =>
            ShootingMinionBodies.Contains(type);

        /// <summary>
        /// 是否为 ① 冲撞型召唤物
        /// (vanilla 召唤物本体,排除 sentry / 模组 / 射击型 / 专属处理)
        /// </summary>
        public static bool IsContactMinion(Projectile projectile)
        {
            if (VanillaMinionExclusions.Contains(projectile.type)) return false;
            if (!projectile.minion) return false;
            if (projectile.sentry) return false;
            if (projectile.type >= ProjectileID.Count) return false;    // 排除模组
            if (IsShootingMinionBody(projectile.type)) return false;
            if (IsSpeciallyHandledMinion(projectile.type)) return false;
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════
        //   ① 冲撞型:SetDefaults 为其启用独立无敌帧
        //   (由主 SetDefaults 调用,蜘蛛走自己的 ApplySpiderSetDefaults)
        // ═══════════════════════════════════════════════════════════════════
        public static void ApplyContactMinionSetDefaults(Projectile projectile)
        {
            if (VanillaMinionExclusions.Contains(projectile.type)) return;
            if (!projectile.minion) return;
            if (projectile.sentry) return;
            if (projectile.type >= ProjectileID.Count) return;
            if (IsShootingMinionBody(projectile.type)) return;
            if (IsSpeciallyHandledMinion(projectile.type)) return;

            // 关闭同类型共享无敌,启用每实例独立无敌
            projectile.usesIDStaticNPCImmunity = false;
            projectile.usesLocalNPCImmunity = true;
            projectile.localNPCHitCooldown = CONTACT_MINION_HIT_COOLDOWN;
        }

        // ═══════════════════════════════════════════════════════════════════
        //   ① 冲撞型:PostAI 扩大范围追踪 (蜘蛛式)
        //
        //   策略:
        //     · 距离玩家 > 5600px → 传送回玩家身边
        //     · 3200px 内有目标 → 覆写 velocity 强力追击
        //     · 其他情况 → 不干预,交给 vanilla AI 处理回归
        // ═══════════════════════════════════════════════════════════════════
        public void ApplyContactMinionTracking(Projectile projectile, Player player)
        {
            float distToPlayer = Vector2.Distance(projectile.Center, player.Center);

            // ── 超出返回阈值:传送回玩家 ──
            if (distToPlayer > CONTACT_MINION_RETURN_DIST)
            {
                projectile.position = player.Center
                    + new Vector2(Main.rand.NextFloat(-80f, 80f), -60f)
                    - projectile.Size * 0.5f;
                projectile.velocity = Vector2.Zero;
                projectile.netUpdate = true;
                return;
            }

            // ── 扩展瞄准 3200px ──
            int targetIdx = AcquireNearestTarget(projectile.Center, CONTACT_MINION_TARGET_RANGE);
            if (targetIdx < 0) return;    // 无目标,交给 vanilla 处理回归

            NPC target = Main.npc[targetIdx];
            Vector2 toTarget = target.Center - projectile.Center;
            float dist = toTarget.Length();
            if (dist < 60f) return;       // 太近,让 vanilla 处理接触手感

            toTarget /= dist;
            projectile.velocity = Vector2.Lerp(
                projectile.velocity,
                toTarget * CONTACT_MINION_CHASE_SPEED,
                0.15f
            );
        }

        // ═══════════════════════════════════════════════════════════════════
        //   ① 冲撞型:OnHitNPC 命中后弹开
        //
        //   独立无敌帧虽已启用,但召唤物贴在 boss 身上时仍会因
        //   localNPCHitCooldown 空转 20 帧。主动弹开可:
        //     · 确保下一次命中间隔稳定
        //     · 让多只召唤物不会挤成一团互相遮挡
        //     · 配合 20 帧无敌帧形成 "打-退-进-打" 的稳定节奏
        //
        //   此方法同时被蜘蛛调用 (蜘蛛本身也是冲撞型)。
        // ═══════════════════════════════════════════════════════════════════
        public static void ApplyContactMinionBounce(Projectile projectile, NPC target)
        {
            Vector2 away = projectile.Center - target.Center;
            if (away.LengthSquared() < 1f)
                away = -Vector2.UnitY;    // 完全重叠时 fallback 向上弹
            else
                away.Normalize();

            projectile.velocity = away * CONTACT_MINION_BOUNCE_SPEED;
            projectile.netUpdate = true;
        }
    }
}
