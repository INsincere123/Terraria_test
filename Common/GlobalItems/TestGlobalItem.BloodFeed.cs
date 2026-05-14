using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalItems
{
    /// <summary>
    /// BloodFeed 系统与 GlobalItem 的交汇点。
    /// 负责暴走/虚弱状态下的攻速修正。
    /// </summary>
    public partial class TestGlobalItem
    {
        // ══════════════════════════════════════════════════════════════
        //   UseTimeMultiplier / UseAnimationMultiplier
        //   暴走：攻速 ×5（useTime × 0.2）
        //   虚弱：攻速 × 0.5（useTime × 2）
        // ══════════════════════════════════════════════════════════════
        public override float UseTimeMultiplier(Item item, Player player)
        {
            var mp = player.GetModPlayer<BloodFeedPlayer>();
            if (mp.IsBerserk)   return 1f / 5f; // ×5 攻速
            if (mp.IsExhausted) return 2f;       // ×0.5 攻速
            return 1f;
        }

        public override float UseAnimationMultiplier(Item item, Player player)
        {
            var mp = player.GetModPlayer<BloodFeedPlayer>();
            if (mp.IsBerserk)   return 1f / 5f;
            if (mp.IsExhausted) return 2f;
            return 1f;
        }
    }
}
