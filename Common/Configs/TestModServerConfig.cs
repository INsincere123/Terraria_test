using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TestMod.Common.Configs
{
    [BackgroundColor(30, 30, 50)]
    public class TestModServerConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;
        public static TestModServerConfig Instance { get; private set; }
        public override void OnLoaded() => Instance = this;
        internal static void ClearInstance() => Instance = null;

        [Header("Prefixes")]
        [DefaultValue(true)]
        public bool EnableRefinementPrefixes { get; set; } = true;

        [DefaultValue(true)]
        public bool EnableFusionPrefix { get; set; } = true;
    }
}
