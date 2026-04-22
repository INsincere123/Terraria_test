using Terraria.ModLoader;

namespace 武器test.Common.Players
{
    public class ThunderBreathingMinionPlayer : ModPlayer
    {
        public bool thunderBreathing;

        public override void ResetEffects()
        {
            // 每帧开始时重置，由仆从 AI 或 Buff.Update 在同帧内重新设为 true
            thunderBreathing = false;
        }
    }
}
