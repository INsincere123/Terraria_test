using System.ComponentModel;
using Terraria.ModLoader.Config;
using TestMod.Common.Systems;

namespace TestMod.Common.Configs
{
    [BackgroundColor(30, 30, 50)]
    public class TestModServerConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;
        public static TestModServerConfig Instance { get; private set; }
        public override void OnLoaded()
        {
            Instance = this;
            // 配置先于内容自动加载；只在这里固定本轮加载的内容开关。
            PrefixAvailabilitySystem.CaptureLoadSettings(EnableRefinementPrefixes, EnableFusionPrefix);
        }
        internal static void ClearInstance() => Instance = null;

        [Header("Prefixes")]
        [ReloadRequired]
        [DefaultValue(true)]
        public bool EnableRefinementPrefixes { get; set; } = true;

        [ReloadRequired]
        [DefaultValue(true)]
        public bool EnableFusionPrefix { get; set; } = true;
    }
}
