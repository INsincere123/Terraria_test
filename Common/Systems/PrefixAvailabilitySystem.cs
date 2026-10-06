using Terraria.ModLoader;
using TestMod.Common.Configs;

namespace TestMod.Common.Systems
{
    // 一轮加载内固定内容开关，配置界面的待重载值不影响正在运行的词条。
    public sealed class PrefixAvailabilitySystem : ModSystem
    {
        internal static bool RefinementEnabled { get; private set; } = true;
        internal static bool FusionEnabled { get; private set; } = true;

        internal static void CaptureLoadSettings(bool refinementEnabled, bool fusionEnabled)
        {
            RefinementEnabled = refinementEnabled;
            FusionEnabled = fusionEnabled;
        }

        public override void Unload()
        {
            RefinementEnabled = FusionEnabled = true;
            TestModServerConfig.ClearInstance();
        }
    }
}
