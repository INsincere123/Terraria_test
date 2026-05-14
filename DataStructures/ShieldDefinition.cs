using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.DataStructures
{
    // ════════════════════════════════════════════════════════════════════════════════
    //   ShieldDefinition — 护盾行为配置结构体
    //
    //   一种护盾 = 一份 ShieldDefinition。多个饰品可以各自提供定义，
    //   ShieldPlayer 会把所有定义的 MaxShield 加算，共享同一个护盾血池。
    //
    //   ── 使用示例（在饰品的 UpdateAccessory 中一行调用）─────────────────────────
    //
    //     ShieldEffect.Apply(player, new ShieldDefinition
    //     {
    //         GetMaxShield        = p => 20f + p.statDefense * 0.10f,
    //         DecayPerSecond      = 0f,                      // 不衰减
    //         RechargeDelayFrames = 5 * 60,                  // 破碎 5 秒后开始恢复
    //         RechargePerSecond   = float.MaxValue,          // 瞬间恢复满
    //         OnActive            = p => p.statDefense += 5, // 护盾存活时 +5 防御
    //         OnBreak             = null,                    // 无破盾特效
    //         ShieldColor         = new Color(80, 160, 255), // 蓝色
    //         ShieldEdgeColor     = new Color(160, 220, 255),// 亮蓝边缘
    //     });
    //
    //   ── 字段说明 ─────────────────────────────────────────────────────────────────
    //
    //   GetMaxShield        — 每帧根据玩家属性计算护盾上限（跟随属性实时变化）
    //
    //   DecayPerSecond      — 每秒自动减少量
    //                         0        = 不衰减，护盾只被受击消耗
    //                         30       = 每秒流失 30 点
    //
    //   RechargeDelayFrames — 护盾归零后，距上次受击多少帧才开始恢复
    //                         300 = 5 秒未受击才恢复
    //
    //   RechargePerSecond   — 恢复速率
    //                         float.MaxValue = 延迟结束后瞬间恢复满（一帧内完成）
    //                         40f            = 每秒渐进恢复 40 点
    //
    //   OnActive            — 护盾 > 0 时每帧执行（在 ResetEffects 阶段）
    //                         适合施加防御加成、速度加成等常驻效果
    //
    //   OnBreak             — 护盾从 > 0 降为 0 的瞬间触发一次
    //                         适合爆炸特效、无敌帧、反弹等
    //
    //   ShieldColor         — 护盾气泡主体颜色（用于 Luminance 滤镜参数）
    //   ShieldEdgeColor     — 护盾气泡边缘颜色（菲涅尔发光区）
    //
    // ════════════════════════════════════════════════════════════════════════════════
    public struct ShieldDefinition
    {
        /// <summary>每帧根据玩家属性计算护盾上限。多个定义的结果加算。</summary>
        public Func<Player, float> GetMaxShield;

        /// <summary>每秒自动减少的护盾量。0 = 不衰减。</summary>
        public float DecayPerSecond;

        /// <summary>护盾归零后，多少帧内未受击才开始恢复（0 = 立刻恢复）。</summary>
        public int RechargeDelayFrames;

        /// <summary>
        /// 恢复速率（每秒，固定值）。
        /// float.MaxValue = 延迟结束后瞬间恢复满血。
        /// 若同时设置了 GetRechargePerSecond，则此字段被忽略。
        /// </summary>
        public float RechargePerSecond;

        /// <summary>
        /// 恢复速率（动态计算，每帧调用，优先于 RechargePerSecond）。
        /// null = 使用 RechargePerSecond 固定值。
        /// 适用于「X 秒恢复满」等随属性变化的场景：
        ///   GetRechargePerSecond = p => GetMaxShield(p) / 2f  →  2 秒恢复满
        /// </summary>
        public Func<Player, float> GetRechargePerSecond;

        /// <summary>护盾 > 0 时每帧调用（ResetEffects 阶段），用于施加属性加成。</summary>
        public Action<Player> OnActive;

        /// <summary>护盾从有血归零的瞬间触发一次，用于破盾特效、无敌帧等。</summary>
        public Action<Player> OnBreak;

        /// <summary>护盾气泡主体颜色（传给 Luminance 滤镜）。</summary>
        public Color ShieldColor;

        /// <summary>护盾气泡边缘/菲涅尔颜色（传给 Luminance 滤镜）。</summary>
        public Color ShieldEdgeColor;
    }
}
