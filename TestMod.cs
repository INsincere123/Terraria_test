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
using TestMod.Common.Systems;

namespace TestMod
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class TestMod : Mod
	{
		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			try
			{
				switch (reader.ReadByte())
				{
					case EnergyShieldPlayer.ShieldStatePacket:
						EnergyShieldPlayer.ReceiveShieldState(reader, whoAmI);
						break;
					case CultistSummonSystem.SummonRequestPacket:
						CultistSummonSystem.ReceiveSummonRequest(reader, whoAmI);
						break;
					case CultistSummonSystem.SummonSoundPacket:
						CultistSummonSystem.ReceiveSummonSound(reader);
						break;
					case MomentumConverterPlayer.ConversionEventPacket:
						MomentumConverterPlayer.ReceiveConversionEvent(reader, whoAmI);
						break;
					case TimeEchoPlayer.RequestPacket:
						TimeEchoPlayer.ReceiveRequest(reader, whoAmI);
						break;
					case TimeEchoPlayer.StatePacket:
						TimeEchoPlayer.ReceiveState(reader);
						break;
					case TimeEchoPlayer.SuccessPacket:
						TimeEchoPlayer.ReceiveSuccess(reader);
						break;
					case Common.GlobalNPCs.GlobalNPC.GuideItemPacket:
						Common.GlobalNPCs.GlobalNPC.ReceiveGuideItem(reader, whoAmI);
						break;
					case TerraArmorPlayer.StatePacket:
						TerraArmorPlayer.ReceiveState(reader, whoAmI);
						break;
				}
			}
			catch (EndOfStreamException)
			{
				Logger.Warn("Ignored truncated TestMod packet.");
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
