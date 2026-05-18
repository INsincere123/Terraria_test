using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
	// ============================================================================
	//  DashKeybinds  ——  冲刺系统键位注册
	// ----------------------------------------------------------------------------
	//  默认绑定:
	//    长冲刺键     (DashKey)        = V 键
	//    短冲刺键     (BlinkKey)       = C 键
	//    单键冲刺键   (VanillaDashKey) = V 键（SingleTapDash 开启时生效）
	//
	//  添加新键位：在 Load 中再调用一次 KeybindLoader.RegisterKeybind。
	// ============================================================================

	public class DashKeybinds : ModSystem
	{
		/// <summary>长冲刺键（默认 V）</summary>
		public static ModKeybind DashKey        { get; private set; }

		/// <summary>短冲刺 / 走位闪现键（默认 C）</summary>
		public static ModKeybind BlinkKey       { get; private set; }

		/// <summary>单键冲刺键（默认 V）：SingleTapDash 开启时，按此键触发 Vanilla 槽冲刺</summary>
		public static ModKeybind VanillaDashKey { get; private set; }

		public override void Load()
		{
			DashKey        = KeybindLoader.RegisterKeybind(Mod, "Omniguardian Dash",  "V");
			BlinkKey       = KeybindLoader.RegisterKeybind(Mod, "Omniguardian Blink", "C");
			VanillaDashKey = KeybindLoader.RegisterKeybind(Mod, "VanillaDash",        "V");
		}

		public override void Unload()
		{
			DashKey        = null;
			BlinkKey       = null;
			VanillaDashKey = null;
		}
	}
}
