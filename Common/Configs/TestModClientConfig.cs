using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TestMod.Common.Configs
{
	[BackgroundColor(30, 30, 50)]
	public class TestModClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static TestModClientConfig Instance { get; private set; }

		public override void OnLoaded() => Instance = this;

		// ── 冲刺 ─────────────────────────────────────────────────────────────

		[Header("Dash")]
		[DefaultValue(false)]
		public bool SingleTapDash { get; set; }
	}
}
