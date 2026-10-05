using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    /// <summary>瞬时追加伤害的同步调用上下文，不保存或联网；投送入口通过 finally 恢复。</summary>
    public class InstantExtraHitPlayer : ModPlayer
    {
        internal bool IsResolvingHit;
    }
}
