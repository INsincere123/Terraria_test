using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Terraria.ModLoader;
using TestMod.Common.Compatibility;
using TestMod.Common.Mechanics.AccessoryEffects;
using TestMod.Common.Players;

namespace TestMod
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class TestMod : Mod
	{
		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			try
			{
				if (reader.ReadByte() == EnergyShieldPlayer.ShieldStatePacket)
					EnergyShieldPlayer.ReceiveShieldState(reader, whoAmI);
			}
			catch (EndOfStreamException)
			{
				Logger.Warn("Ignored truncated shield state packet.");
			}
		}

		public override void Load()
		{
			OnHitEffectsPlayer.LoadRegistry();
			// ... 其他 Load 内容
		}

		public override void Unload()
		{
			TextRenderingBridge.Unload();
			OnHitEffectsPlayer.UnloadRegistry();
			// ... 其他 Unload 内容
		}

		public override void PostSetupContent()
		{
			TextRenderingBridge.Load();
		}
	}
}
