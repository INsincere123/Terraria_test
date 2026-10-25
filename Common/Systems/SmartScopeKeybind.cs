using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public class SmartScopeKeybind : ModSystem
    {
        public static ModKeybind ActivateKey { get; private set; }
        public override void Load()
            => ActivateKey = KeybindLoader.RegisterKeybind(Mod, "SmartScope", "None");
        public override void Unload() => ActivateKey = null;
    }
}
