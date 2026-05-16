using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public class SuperRelocatorKeybind : ModSystem
    {
        public static ModKeybind Hotkey { get; private set; }

        public override void Load()
            => Hotkey = KeybindLoader.RegisterKeybind(Mod, "SuperRelocator", "V");

        public override void Unload()
            => Hotkey = null;
    }
}
