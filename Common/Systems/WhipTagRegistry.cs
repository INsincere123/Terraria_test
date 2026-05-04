using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 单个鞭子 tag 的效果参数。
    /// </summary>
    public struct WhipTagData
    {
        /// <summary>额外暴击概率（0-100）。</summary>
        public int CritChance;

        /// <summary>固定标记伤害加成。</summary>
        public float FlatDamage;

        /// <summary>是否在召唤物命中时触发爆炸。</summary>
        public bool HasExplosion;

        /// <summary>爆炸伤害 = 召唤物本次伤害 × 此比例。</summary>
        public float ExplosionDamageRatio;

        /// <summary>爆炸触发后的冷却帧数，防止每帧都炸。</summary>
        public int CooldownFrames;
    }

    /// <summary>
    /// 鞭子 tag 注册表。
    ///
    /// ══════════════════════════════════════════════════════════════
    /// 【新增一把鞭子的 tag 效果】只需在 PostSetupContent() 里加一行 Register：
    ///
    ///   Register(ModContent.BuffType&lt;你的TagBuff&gt;(), new WhipTagData
    ///   {
    ///       CritChance           = 30,     // 额外暴击率（0-100）
    ///       FlatDamage           = 15f,    // 固定标记伤害
    ///       HasExplosion         = true,   // 是否触发爆炸
    ///       ExplosionDamageRatio = 0.6f,   // 爆炸伤害比例
    ///       CooldownFrames       = 90,     // 爆炸冷却帧数（60帧=1秒）
    ///   });
    ///
    /// 【只加暴击/伤害，不要爆炸】把 HasExplosion = false 或直接省略即可：
    ///
    ///   Register(BuffID.某原版TagBuff, new WhipTagData
    ///   {
    ///       CritChance = 50,
    ///   });
    ///
    /// 【修改已有鞭子参数】直接改下方对应的 Register 调用里的数值即可。
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    public class WhipTagRegistry : ModSystem
    {
        /// <summary>buffType → 效果参数</summary>
        public static Dictionary<int, WhipTagData> Tags { get; private set; } = new();

        public static void Register(int buffType, WhipTagData data)
            => Tags[buffType] = data;

        public override void PostSetupContent()
        {
            // ── 自制鞭子 ──────────────────────────────────────────────
            Register(ModContent.BuffType<TestWhipTagBuff>(), new WhipTagData
            {
                CritChance           = 50,
                FlatDamage           = 10f,
                HasExplosion         = true,
                ExplosionDamageRatio = 0.5f,
                CooldownFrames       = 60,
            });

            // ── 原版暗黑收割（仅加暴击，原版爆炸灵魂效果由 vanilla 自带） ──
            Register(BuffID.ScytheWhipEnemyDebuff, new WhipTagData
            {
                CritChance = 50,
            });
        }

        public override void Unload()
        {
            Tags.Clear();
        }
    }
}
