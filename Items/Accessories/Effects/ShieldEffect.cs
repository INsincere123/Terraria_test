using Terraria;
using TestMod.Common.Players;
using TestMod.DataStructures;

namespace TestMod.Items.Accessories.Effects
{
    /// <summary>
    /// 护盾效果注入入口。
    ///
    /// 在饰品的 UpdateAccessory() 中调用 Apply()，
    /// EnergyShieldPlayer 将在 PostUpdateMiscEffects 阶段收集并汇总所有护盾定义。
    ///
    /// 多个饰品同时装备时，各自的 GetMaxShield 加算，共享同一护盾血池。
    ///
    /// 示例（饰品文件里）：
    ///   public override void UpdateAccessory(Player player, bool hideVisual)
    ///   {
    ///       ShieldEffect.Apply(player, new ShieldDefinition
    ///       {
    ///           GetMaxShield        = p => 20f + p.statDefense * 0.10f,
    ///           DecayPerSecond      = 0f,
    ///           RechargeDelayFrames = 5 * 60,
    ///           RechargePerSecond   = float.MaxValue,
    ///           OnActive            = p => p.statDefense += 5,
    ///           ShieldColor         = new Color(80, 160, 255),
    ///           ShieldEdgeColor     = new Color(160, 220, 255),
    ///       });
    ///   }
    /// </summary>
    public static class ShieldEffect
    {
        /// <summary>
        /// 向当前帧的护盾系统注入一份护盾定义。
        /// 在 UpdateAccessory() 中调用；每帧都会重新注入（ResetEffects 清空）。
        /// </summary>
        public static void Apply(Player player, ShieldDefinition def)
        {
            player.GetModPlayer<EnergyShieldPlayer>().AddDefinition(def);
        }
    }
}
