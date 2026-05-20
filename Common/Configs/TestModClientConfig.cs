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

		// ── 护盾 UI ───────────────────────────────────────────────────────

		[Header("ShieldUI")]
		[DefaultValue(true)]
		public bool ShowStandaloneShieldBar { get; set; }

		[Range(-1000, 1000)]
		[DefaultValue(0)]
		public int StandaloneShieldBarOffsetX { get; set; }

		[Range(-600, 600)]
		[DefaultValue(0)]
		public int StandaloneShieldBarOffsetY { get; set; }
	}
}
