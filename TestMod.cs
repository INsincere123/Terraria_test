using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.AccessoryEffects;

namespace TestMod
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class TestMod : Mod
	{
		public override void Load()
		{
			OnHitEffectsPlayer.LoadRegistry();
			// ... 其他 Load 内容
		}

		public override void Unload()
		{
			OnHitEffectsPlayer.UnloadRegistry();
			// ... 其他 Unload 内容
		}
	}
}
