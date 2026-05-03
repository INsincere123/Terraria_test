using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
	// ============================================================================
	//  OmniKeybinds  ——  键位注册
	// ----------------------------------------------------------------------------
	//  注册自定义键位, 玩家可以在游戏的 "设置 → 控件" 里自由修改。
	//
	//  默认绑定:
	//    冲刺键 (DashKey)    = V 键
	//    闪现键 (BlinkKey)   = C 键
	//
	//  添加新键位的方法:
	//    在 Load 中再调用一次 KeybindLoader.RegisterKeybind, 并加一个 public static 字段。
	// ============================================================================

	public class OmniKeybinds : ModSystem
	{
		/// <summary>长冲刺键 (默认 V), 玩家可在控件设置中自由修改</summary>
		public static ModKeybind DashKey  { get; private set; }

		/// <summary>短冲刺 / 走位闪现键 (默认 C), 玩家可在控件设置中自由修改</summary>
		public static ModKeybind BlinkKey { get; private set; }

		public override void Load()
		{
			DashKey  = KeybindLoader.RegisterKeybind(Mod, "Omniguardian Dash",  "V");
			BlinkKey = KeybindLoader.RegisterKeybind(Mod, "Omniguardian Blink", "C");
		}

		public override void Unload()
		{
			DashKey  = null;
			BlinkKey = null;
		}
	}
}