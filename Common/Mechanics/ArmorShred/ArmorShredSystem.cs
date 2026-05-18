using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace TestMod.Common.Mechanics.ArmorShred
{
    /// <summary>
    /// 破甲层数数据管理系统。
    ///
    /// 职责：持有层数字典，提供公共 API，负责世界卸载时清理数据。
    /// 读写双方（PhantasmSpecialArrowProj、GlobalNPC）只与本类交互，
    /// 互相不直接依赖。
    ///
    /// 层数上限 10 层，每层降低目标 10 点防御。
    /// </summary>
    public class ArmorShredSystem : ModSystem
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const int MaxStacks       = 10;  // 最大层数
        public const int DefensePerStack = 10;  // 每层减少防御值
        // ─────────────────────────────────────────────────────────────

        private static readonly Dictionary<int, int> _stacks = new();

        // ══════════════════════════════════════════════════════════════
        //   公共 API
        // ══════════════════════════════════════════════════════════════

        /// <summary>返回指定 NPC 当前的破甲层数（0 = 无）。</summary>
        public static int GetStacks(int npcWhoAmI)
            => _stacks.TryGetValue(npcWhoAmI, out int s) ? s : 0;

        /// <summary>
        /// 为指定 NPC 叠加一层破甲，不超过 MaxStacks。
        /// 应在命中时调用，并配合 AddBuff(ArmorShredDebuff) 保持层数存活。
        /// </summary>
        public static void AddStack(int npcWhoAmI)
        {
            int current = GetStacks(npcWhoAmI);
            _stacks[npcWhoAmI] = Math.Min(current + 1, MaxStacks);
        }

        /// <summary>
        /// 移除指定 NPC 的破甲层数记录。
        /// 在 NPC 死亡或 buff 消失时调用。
        /// </summary>
        public static void Remove(int npcWhoAmI)
            => _stacks.Remove(npcWhoAmI);

        // ══════════════════════════════════════════════════════════════
        //   世界生命周期：卸载时清理字典防止跨世界残留
        // ══════════════════════════════════════════════════════════════
        public override void OnWorldUnload() => _stacks.Clear();
    }
}
