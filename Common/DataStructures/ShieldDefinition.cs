using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.Common.DataStructures
{
    /// <summary>
    /// 每个来源提供上限、加成和视觉配置，普通量合并为一个池。
    /// 恢复由核心统一管理：11 秒未受击后，每秒恢复普通总上限的 1/6。
    /// </summary>
    public struct ShieldDefinition
    {
        /// <summary>PostUpdateMiscEffects 每帧计算；多个来源的普通护盾上限加算。</summary>
        public Func<Player, float> GetMaxShield;
        /// <summary>普通量每秒自动减少值；0 表示只被受击消耗，多个来源取最大。</summary>
        public float DecayPerSecond;
        /// <summary>总护盾量大于零时，在 PostUpdateMiscEffects 阶段施加属性加成。</summary>
        public Action<Player> OnActive;
        /// <summary>总护盾量从正值归零时调用一次；卸装和死亡不触发战斗破盾效果。</summary>
        public Action<Player> OnBreak;
        public Color ShieldColor;
        public Color ShieldEdgeColor;
    }
}
