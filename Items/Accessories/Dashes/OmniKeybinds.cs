using Terraria.ModLoader;

namespace 武器test.Items.Accessories.Dashes
{
	// ============================================================================
	//  OmniKeybinds  ——  键位注册
	// ----------------------------------------------------------------------------
	//  注册自定义键位, 玩家可以在游戏的 "设置 → 控件" 里自由修改。
	//
	//  默认绑定:
	//    冲刺键 (DashKey)    = X 键
	//
	//  添加新键位的方法:
	//    在 Load 中再调用一次 KeybindLoader.RegisterKeybind, 并加一个 public static 字段。
	// ============================================================================

	public class OmniKeybinds : ModSystem
	{
		/// <summary>冲刺键 (默认 X), 玩家可在控件设置中自由修改</summary>
		public static ModKeybind DashKey { get; private set; }

		public override void Load()
		{
			DashKey = KeybindLoader.RegisterKeybind(Mod, "Omniguardian Dash", "X");
		}

		public override void Unload()
		{
			DashKey = null;
		}
	}
}
