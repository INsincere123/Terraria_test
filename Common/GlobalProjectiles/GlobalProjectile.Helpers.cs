using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using TestMod.Content.Buffs;
using TestMod.Common.Utilities;
using TestMod.Common.Mechanics.AccessoryEffects;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   索敌辅助（委托给 TargetUtils，保留此处仅为向后兼容）
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 在 maxRange 内找最近的可追踪 NPC。
        /// 内部委托 TargetUtils.FindNearest，新代码请直接调用 TargetUtils。
        /// </summary>
        private static int AcquireNearestTarget(Vector2 center, float maxRange)
            => TargetUtils.FindNearest(center, maxRange);

        // ══════════════════════════════════════════════════════════════
        //   收割：AOE 伤害 + 玩家 buff
        //   (副作用操作，留在此处而非 TargetUtils/DrawUtils)
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 以 center 为圆心，400f 内最多 6 个敌人各受基础 66 伤害（吃一次通用增伤），
        /// 玩家获得 HarvestTimeBuff（3 秒）。
        /// </summary>
        private static void DoHarvest(NPC center, Player player)
        {
            ExtraHitConfig damageConfig = new()
            {
                FlatDamage = 66f,
                FixedClass = DamageClass.Generic,
                HitDirection = 0,
                UseVanillaCombatText = true,
            };
            int count = 0;
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;
                if (Vector2.Distance(center.Center, npc.Center) < 400f)
                {
                    ExtraHitEffect.Strike(player, npc, damageConfig);
                    if (++count >= 6) break;
                }
            }
            player.AddBuff(ModContent.BuffType<HarvestTimeBuff>(), 180);
        }

        // ══════════════════════════════════════════════════════════════
        //   视觉辅助（委托给 DrawUtils）
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 在 from→to 之间生成链式电弧粒子线（仅客户端）。
        /// 内部委托 DrawUtils.SpawnDustLine，新代码请直接调用 DrawUtils。
        /// </summary>
        private static void SpawnSplitVisual(Vector2 from, Vector2 to)
            => DrawUtils.SpawnDustLine(from, to, Microsoft.Xna.Framework.Color.Cyan);
    }
}
