using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 精确飞行系统的按键。注册在自己的 ModSystem 里，不污染其他 keybind 文件。
    /// </summary>
    public class PreciseFlightKeybinds : ModSystem
    {
        public static ModKeybind ToggleHotkey { get; private set; }
        public static ModKeybind SlowdownHotkey { get; private set; }

        public override void Load()
        {
            // 默认按键（玩家可在 设置 -> 控件 中修改）
            ToggleHotkey = KeybindLoader.RegisterKeybind(Mod, "PreciseFlightToggle", "G");
            SlowdownHotkey = KeybindLoader.RegisterKeybind(Mod, "PreciseFlightSlowdown", "RightShift");
        }

        public override void Unload()
        {
            ToggleHotkey = null;
            SlowdownHotkey = null;
        }
    }
}
