using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.Common.Utilities
{
    public static class TargetUtils
    {
        /// <summary>
        /// 找最近的可追踪 NPC，跳过命中冷却中的敌人
        /// 这样穿透后会自动转向下一个目标而不是粘着刚命中的同一个
        /// 此方法自带高阶追踪，无需ApplyHighTierTracking
        /// </summary>
        public static int FindNearestTargetNotOnCooldown(
            Vector2 center,
            float maxDistance)
        {
            int target = -1;
            float sqrMaxDistance = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || npc.friendly || npc.dontTakeDamage)
                    continue;

                float sqrDistance =
                    Vector2.DistanceSquared(center, npc.Center);

                if (sqrDistance < sqrMaxDistance)
                {
                    sqrMaxDistance = sqrDistance;
                    target = i;
                }
            }

            return target;
        }
    }
}