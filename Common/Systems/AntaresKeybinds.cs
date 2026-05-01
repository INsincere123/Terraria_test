using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    // ============================================================================
    //  AntaresKeybinds  ——  心宿二套装键位注册
    // ----------------------------------------------------------------------------
    //  注册自定义键位，玩家可以在游戏的 "设置 → 控件" 里自由修改。
    //
    //  默认绑定:
    //    引力井 (GravityWellKey) = Z 键
    //
    //  添加新键位的方法:
    //    在 Load 中再调用一次 KeybindLoader.RegisterKeybind，并加一个 public static 字段。
    // ============================================================================

    public class AntaresKeybinds : ModSystem
    {
        /// <summary>引力井主动技能键 (默认 Z)，玩家可在控件设置中自由修改</summary>
        public static ModKeybind GravityWellKey { get; private set; }

        public override void Load()
        {
            GravityWellKey = KeybindLoader.RegisterKeybind(Mod, "Antares Gravity Well", "Z");
        }

        public override void Unload()
        {
            GravityWellKey = null;
        }
    }
}
