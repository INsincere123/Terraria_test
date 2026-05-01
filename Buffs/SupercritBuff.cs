using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Buffs
{
    /// <summary>
    /// 超暴击 Buff
    /// 挂着此 Buff 时，玩家溢出 100% 的暴击率会按 1% 暴击率 → 2% 暴击伤害 的比例
    /// 转换为额外的暴击伤害倍率，并在弹幕命中 NPC 时生效。
    /// 任何装备 / 药水都可通过 player.AddBuff(ModContent.BuffType<SupercritBuff>(), ...) 来引用此机制。
    /// </summary>
    public class SupercritBuff : ModBuff
    {
        // ============== 可调参数 ==============

        /// <summary>
        /// 1% 溢出暴击率 转换为 多少% 暴击伤害。默认 2.0，即 1% 暴击率 = +2% 暴击伤害。
        /// </summary>
        public const float CritOverflowToCritDamageRatio = 2f;

        // ======================================

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<SupercritPlayer>().supercritEnabled = true;
        }

    }
}