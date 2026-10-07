using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public sealed class MomentumConverterKeybind : ModSystem
    {
        public static ModKeybind ActivateKey { get; private set; }

        public override void Load()
            => ActivateKey = KeybindLoader.RegisterKeybind(Mod, "MomentumConversion", "F");

        public override void Unload()
            => ActivateKey = null;
    }
}
