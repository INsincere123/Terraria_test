using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace 武器test
{
    public partial class MyGlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   辅助方法
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 以 center 为圆心，在 maxRange 内找最近的可追踪 NPC
        /// </summary>
        private int AcquireNearestTarget(Vector2 center, float maxRange)
        {
            int bestIndex    = -1;
            float bestDistSq = maxRange * maxRange;

            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;

                float distSq = Vector2.DistanceSquared(center, npc.Center);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex  = i;
                }
            }
            return bestIndex;
        }

        /// <summary>
        /// 收割：400f 内最多 6 个敌人各受 66 伤害，玩家获得 HarvestTimeBuff
        /// </summary>
        private void DoHarvest(NPC center, Player player)
        {
            int count = 0;
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;
                if (Vector2.Distance(center.Center, npc.Center) < 400f)
                {
                    npc.SimpleStrikeNPC(66, 50);
                    if (++count >= 6) break;
                }
            }
            player.AddBuff(ModContent.BuffType<HarvestTimeBuff>(), 180);
        }

        /// <summary>
        /// 链式溅射的青色电弧粒子线（仅客户端）
        /// </summary>
        private void SpawnSplitVisual(Vector2 from, Vector2 to)
        {
            if (Main.netMode == NetmodeID.Server) return;

            Dust.QuickDustLine(from, to, 10f, Color.Cyan);

            for (int i = 0; i < 6; i++)
            {
                Vector2 pos = Vector2.Lerp(from, to, i / 5f);
                Dust.NewDustPerfect(pos, DustID.Electric, Vector2.Zero, 0, Color.Cyan, 1.1f);
            }
            Dust.NewDustPerfect(to, DustID.Electric, Vector2.Zero, 0, Color.White, 1.4f);
        }
    }
}
