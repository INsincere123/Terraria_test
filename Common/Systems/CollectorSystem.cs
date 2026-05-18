using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Utilities;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 收集者系统：追踪 Boss 血量累计、发放处决奖励。
    ///
    /// ── Boss 完全死亡的判定（为何用 PostUpdateEverything）────────────────
    ///   月亮领主等 Boss 死亡时有动画延迟：核心 OnKill 触发时，手和头的 NPC
    ///   仍处于 active=true 状态，在 OnKill 里检查"其他 Boss 是否存活"会
    ///   因死亡动画中的手/头而提前 return，导致奖励永远无法发放。
    ///
    ///   改为在 PostUpdateEverything 里检测每帧：
    ///     上一帧有 Boss → 这一帧没有 Boss → Boss 战刚结束 → 发放奖励
    ///   这样完全规避动画延迟，月亮领主等 Boss 的死亡动画结束后才触发，
    ///   对玩家感知无明显影响（动画本身几秒内结束）。
    ///
    /// ── 奖励规则 ────────────────────────────────────────────────────
    ///   · 铂金 + 金币：CurveUtils.ComputeBossRewardPlatinum(totalHp)
    ///   · Lucky Buff：20 分钟
    ///   · 仅发放给装备了收集者的存活玩家
    /// </summary>
    public class CollectorSystem : ModSystem
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        private const int LuckyBuffDuration = 20 * 60 * 60; // 20 分钟（帧数）
        // ─────────────────────────────────────────────────────────────

        // 本场 Boss 战中所有死亡 Boss 部件 lifeMax 的累计值
        private static int _bossHpAccumulator;

        // 上一帧是否有存活的 Boss（用于检测 Boss 战结束瞬间）
        private static bool _hadBossLastFrame;

        // ── OnKill：仅负责累加 HP，不做"全部死亡"判断 ────────────────
        /// <summary>由 GlobalNPC.OnKill 调用，累加 Boss 血量。</summary>
        public static void OnNpcKilled(NPC npc)
        {
            if (!npc.boss) return;
            _bossHpAccumulator += npc.lifeMax;
        }

        // ── PostUpdateEverything：检测 Boss 战结束瞬间 ────────────────
        /// <summary>
        /// 每帧检测场上是否有存活 Boss。
        /// 从"有"变为"无"的那一帧（死亡动画结束后），发放奖励。
        /// </summary>
        public override void PostUpdateEverything()
        {
            bool anyBossAlive = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.boss) { anyBossAlive = true; break; }
            }

            // 上一帧有 Boss，这一帧没有 → Boss 战刚结束
            if (_hadBossLastFrame && !anyBossAlive && _bossHpAccumulator > 0)
            {
                int totalHp        = _bossHpAccumulator;
                _bossHpAccumulator = 0;
                GiveReward(totalHp);
            }

            _hadBossLastFrame = anyBossAlive;
        }

        // ── 奖励发放 ─────────────────────────────────────────────────
        private static void GiveReward(int totalHp)
        {
            float reward   = CurveUtils.ComputeBossRewardPlatinum(totalHp);
            int   platinum = (int)reward;
            int   gold     = (int)Math.Round((reward - platinum) * 100f);

            var src = new EntitySource_Misc("CollectorReward");

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (!player.active || player.dead) continue;
                if (!player.GetModPlayer<CollectorPlayer>().IsEquipped) continue;

                if (platinum > 0)
                    player.QuickSpawnItem(src, ItemID.PlatinumCoin, platinum);
                if (gold > 0)
                    player.QuickSpawnItem(src, ItemID.GoldCoin, gold);

                player.AddBuff(BuffID.Lucky, LuckyBuffDuration);

                Main.NewText($"获得{platinum} 铂金 + {gold} 金币",
                    new Microsoft.Xna.Framework.Color(255, 255, 100));
            }
        }

        // ── 世界卸载时清理，防止跨存档状态污染 ──────────────────────
        public override void OnWorldUnload()
        {
            _bossHpAccumulator = 0;
            _hadBossLastFrame  = false;
        }
    }
}