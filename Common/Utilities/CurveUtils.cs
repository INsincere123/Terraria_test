using System;
using Terraria.Utilities;

namespace TestMod.Common.Utilities
{
    /// <summary>
    /// 无状态数学工具：曲线公式、加权随机。
    /// 所有方法均为纯函数，无任何游戏状态依赖，可独立单元测试。
    /// </summary>
    public static class CurveUtils
    {
        // ══════════════════════════════════════════════════════════════
        //   曲线公式
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 标准 Sigmoid 函数：输出范围 (0, 1)。
        /// x 越大输出趋近 1，越小趋近 0。
        /// steepness  — 斜率（越大曲线越陡）
        /// midpoint   — 输出 0.5 时的 x 值
        /// </summary>
        public static float Sigmoid(float x, float steepness = 0.45f, float midpoint = 15f)
            => 1f / (1f + MathF.Exp(-steepness * (x - midpoint)));

        /// <summary>
        /// 幂次伤害曲线：ratio=0 → 返回 1.0，ratio=1 → 返回 1+maxBonus。
        /// 指数越高，低段平缓、高段陡峭。
        /// 适合"血量越低伤害越高"的非线性增益场景。
        /// ratio 自动 clamp 到 [0, 1]。
        /// </summary>
        public static float PowerCurve(float ratio, float exponent, float maxBonus)
            => 1f + maxBonus * MathF.Pow(Math.Clamp(ratio, 0f, 1f), exponent);

        /// <summary>
        /// 对数增长：从 startStack 到 endStack 之间用对数从 0 增长到 1。
        /// 适合"叠层越多增速越慢"的设计，如剑气后期叠层伤害。
        /// </summary>
        public static float LogCurve(int stacks, int startStack, int endStack)
        {
            if (stacks <= startStack) return 0f;
            float range = endStack - startStack;
            return Math.Clamp(MathF.Log(1f + stacks - startStack) / MathF.Log(1f + range), 0f, 1f);
        }

        // ══════════════════════════════════════════════════════════════
        //   Boss 奖励曲线
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 根据 Boss 最大生命值计算铂金奖励（对数曲线）。
        ///
        /// 映射关系：
        ///   HP ≤ 2,000      → 3 铂金（下限）
        ///   HP ≥ 1,000,000  → 60 铂金（上限）
        ///   中间段按对数插值：先快后慢，自动兼容各模组 Boss
        ///
        /// 返回值保留两位小数：
        ///   整数部分 = 铂金币数量
        ///   小数×100  = 金币数量（如 15.73 → 15 铂 + 73 金）
        /// </summary>
        public static float ComputeBossRewardPlatinum(int lifeMax)
        {
            const int   MinHp     = 2_000;
            const int   MaxHp     = 1_000_000;
            const float MinReward = 3f;
            const float MaxReward = 60f;

            int   hp = Math.Clamp(lifeMax, MinHp, MaxHp);
            float t  = MathF.Log((float)hp / MinHp) / MathF.Log((float)MaxHp / MinHp); // [0,1]
            float raw = MinReward + (MaxReward - MinReward) * t;

            return (float)Math.Round(raw, 2); // 保留两位小数
        }

        // ══════════════════════════════════════════════════════════════
        //   加权随机
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 在 pool 中按权重随机返回一个 id。
        /// pool 格式：(id, weight) — weight 越大被选中概率越高。
        /// O(n) 线性扫描，适合池子较小（百级以内）的场景。
        /// pool 为空时返回 -1。
        /// </summary>
        public static int WeightedRandom(UnifiedRandom rand, (int id, int weight)[] pool)
        {
            if (pool == null || pool.Length == 0) return -1;

            int total = 0;
            foreach (var (_, w) in pool) total += w;
            if (total <= 0) return pool[0].id;

            int roll = rand.Next(total);
            int cum  = 0;
            foreach (var (id, w) in pool)
            {
                cum += w;
                if (roll < cum) return id;
            }
            return pool[0].id; // 安全兜底
        }
    }
}
