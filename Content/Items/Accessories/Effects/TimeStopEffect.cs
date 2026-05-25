using TestMod.Common.Players;
using Terraria;

namespace TestMod.Content.Items.Accessories.Effects
{
    /// <summary>
    /// 时停能力的赋予入口。
    /// 任何饰品 / 盔甲套装在 UpdateAccessory / UpdateEquips / UpdateArmorSet 里调用：
    ///     TimeStopEffect.Grant(player);
    /// 即可让该玩家本帧获得时停能力（按键可用）。
    /// 由于 TimeStopPlayer.ResetEffects 每帧清零 HasTimeStopAbility，
    /// 装备一旦卸下，能力自然失效。
    /// </summary>
    public static class TimeStopEffect
    {
        /// <summary>
        /// 赋予指定玩家本帧的时停能力。
        /// </summary>
        public static void Grant(Player player)
        {
            if (player == null || !player.active) return;
            player.GetModPlayer<TimeStopPlayer>().HasTimeStopAbility = true;
        }
    }
}
