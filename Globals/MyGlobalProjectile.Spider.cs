using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace 武器test
{
    public partial class MyGlobalProjectile
    {
        // ═══════════════════════════════════════════════════════════════
        //   蜘蛛法杖 (QueenSpiderStaff) 召唤物强化
        //
        //   涉及弹射物：
        //     390 VenomSpider     毒液蜘蛛
        //     391 JumperSpider    跳跃蜘蛛
        //     392 DangerousSpider 危险蜘蛛
        //
        //   原版瓶颈：
        //     · 15 帧同类型静态无敌帧 → 多蜘蛛时打不满伤害
        //     · 瞄准范围 50 图格 (800px)
        //     · 自动返回玩家阈值 87.5 图格 (1400px)
        //
        //   本 mod 改动：
        //     · 独立无敌帧：每只蜘蛛独立命中冷却，不共享类型无敌
        //     · 瞄准范围扩展：800px → 3200px (200 图格)
        //     · 返回阈值扩展：1400px → 5600px (350 图格，×4)
        // ═══════════════════════════════════════════════════════════════

        private const float SPIDER_TARGET_RANGE = 3200f;  // 扩展后瞄准距离
        private const float SPIDER_RETURN_DIST  = 5600f;  // 扩展后返回阈值 (4× 原版)
        private const int   SPIDER_HIT_COOLDOWN = 20;     // 20 帧 (匹配原版 0.33s 内部冷却)
        private const float SPIDER_VANILLA_RETURN = 1400f; // 原版返回阈值

        public static bool IsSpiderMinion(int type) =>
            type == ProjectileID.VenomSpider       // 390
         || type == ProjectileID.JumperSpider      // 391
         || type == ProjectileID.DangerousSpider;  // 392

        // ═══════════════════════════════════════════════════════════════
        //   SetDefaults 子步骤：为蜘蛛启用独立无敌帧
        //   由主 SetDefaults 调用
        // ═══════════════════════════════════════════════════════════════
        public static void ApplySpiderSetDefaults(Projectile projectile)
        {
            if (!IsSpiderMinion(projectile.type)) return;

            // 关闭同类型共享无敌 (原版 15 帧)
            projectile.usesIDStaticNPCImmunity = false;

            // 启用每实例独立无敌 (每只蜘蛛独立计数)
            projectile.usesLocalNPCImmunity = true;
            projectile.localNPCHitCooldown  = SPIDER_HIT_COOLDOWN;
        }

        // ═══════════════════════════════════════════════════════════════
        //   PostAI 子步骤：扩展瞄准范围 + 延长返回阈值
        //   由主 PostAI 的蜘蛛分支调用
        //
        //   策略（不替换 vanilla AI，只覆写 velocity）：
        //     · 有目标 (范围 3200px)：覆写 velocity 追击
        //     · 无目标但 > 1400px：velocity 衰减，阻止 vanilla 拉回
        //     · > 5600px：强制传送回玩家
        // ═══════════════════════════════════════════════════════════════
        public static void ApplySpiderPostAI(Projectile projectile, Player player)
        {
            if (!IsSpiderMinion(projectile.type)) return;

            float distToPlayer = Vector2.Distance(projectile.Center, player.Center);

            // ── 超出 4× 阈值：传送回玩家身旁 ──
            if (distToPlayer > SPIDER_RETURN_DIST)
            {
                projectile.position = player.Center
                    + new Vector2(Main.rand.NextFloat(-80f, 80f), -60f)
                    - projectile.Size * 0.5f;
                projectile.velocity = Vector2.Zero;
                projectile.netUpdate = true;
                return;
            }

            // ── 扩展瞄准：在 3200px 范围寻敌 ──
            int targetIdx = FindSpiderTarget(projectile.Center, SPIDER_TARGET_RANGE);

            if (targetIdx >= 0)
            {
                // 有目标：覆写速度追击
                NPC target = Main.npc[targetIdx];
                Vector2 toTarget = target.Center - projectile.Center;
                float dist = toTarget.Length();

                if (dist > 60f)
                {
                    toTarget /= dist;
                    const float chaseSpeed = 20f;   // 20f 追击速度（原版约 14f，适当提升以覆盖更大范围）
                    projectile.velocity = Vector2.Lerp(
                        projectile.velocity,
                        toTarget * chaseSpeed,
                        0.15f
                    );
                }
                // 近距离 (<60px) 交给 vanilla 处理接触伤害手感
            }
            else if (distToPlayer > SPIDER_VANILLA_RETURN)
            {
                // 无目标但已超出原版返回阈值：阻止 vanilla 拉回
                // 让蜘蛛在扩展区域 (1400~5600px) 内悬停等待新目标
                projectile.velocity *= 0.9f;
            }
            // 其他情况 (距离玩家 < 1400 且无目标)：不干预，vanilla 正常悬停
        }

        // ───────────────────────────────────────────────────────────────
        //   寻敌：取最近可追击 NPC
        // ───────────────────────────────────────────────────────────────
        private static int FindSpiderTarget(Vector2 from, float maxRange)
        {
            int bestIdx = -1;
            float bestDistSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;

                float distSq = Vector2.DistanceSquared(from, npc.Center);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }
    }
}
