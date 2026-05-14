using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.Common.Utilities
{
    /// <summary>
    /// 无状态索敌工具。所有方法均不修改任何游戏状态。
    /// </summary>
    public static class TargetUtils
    {
        // ══════════════════════════════════════════════════════════════
        //   基础索敌
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 在 maxRange 范围内找最近的可被追踪 NPC（使用 CanBeChasedBy 标准）。
        /// 返回 NPC 索引，找不到返回 -1。
        /// </summary>
        public static int FindNearest(Vector2 center, float maxRange)
        {
            int   best   = -1;
            float bestSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;

                float distSq = Vector2.DistanceSquared(center, npc.Center);
                if (distSq < bestSq)
                {
                    bestSq = distSq;
                    best   = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 在 maxRange 范围内找最近的可被追踪 NPC，要求有视线（无遮挡墙壁）。
        /// 返回 NPC 索引，找不到返回 -1。
        /// </summary>
        public static int FindNearestWithLineOfSight(Vector2 center, float maxRange)
        {
            int   best   = -1;
            float bestSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;

                float distSq = Vector2.DistanceSquared(center, npc.Center);
                if (distSq >= bestSq) continue;

                if (!Collision.CanHitLine(center, 1, 1, npc.Center, 1, 1)) continue;

                bestSq = distSq;
                best   = i;
            }
            return best;
        }

        /// <summary>
        /// 返回 maxRange 内所有可被追踪 NPC 的索引列表（无顺序保证）。
        /// 适用于 AOE 伤害、群体扫描等场景。
        /// </summary>
        public static List<int> FindAllInRange(Vector2 center, float maxRange)
        {
            var   result    = new List<int>();
            float maxRangeSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;
                if (Vector2.DistanceSquared(center, npc.Center) <= maxRangeSq)
                    result.Add(i);
            }
            return result;
        }

        // ══════════════════════════════════════════════════════════════
        //   带排除条件的索敌
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 在 maxRange 内找最近的可被追踪 NPC，跳过 excludeIndex 指定的目标。
        /// 用于多段弹幕"跳过已命中的目标"的链式追踪。
        /// </summary>
        public static int FindNearestExcluding(Vector2 center, float maxRange, int excludeIndex)
        {
            int   best   = -1;
            float bestSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (i == excludeIndex) continue;
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;

                float distSq = Vector2.DistanceSquared(center, npc.Center);
                if (distSq < bestSq)
                {
                    bestSq = distSq;
                    best   = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 兼容旧代码的别名。等同于 FindNearest，但过滤条件略宽松（不含 CanBeChasedBy 的 lifeMax 检查）。
        /// 新代码请直接使用 FindNearest。
        /// </summary>
        public static int FindNearestTargetNotOnCooldown(Vector2 center, float maxDistance)
            => FindNearest(center, maxDistance);
    }
}
