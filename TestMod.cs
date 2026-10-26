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
					case GodModePlayer.StatePacket:
						GodModePlayer.ReceiveState(reader, whoAmI);
						break;
					case BaseHealthPlayer.StatePacket:
						BaseHealthPlayer.ReceiveState(reader, whoAmI);
						break;
					case LifetakingPlayer.RequestPacketId:
						LifetakingPlayer.ReceiveRequest(reader, whoAmI);
						break;
					case LifetakingPlayer.StatePacketId:
						LifetakingPlayer.ReceiveState(reader);
						break;
					case ScarecrowSystem.PlaceRequestPacket:
						ScarecrowSystem.ReceivePlaceRequest(reader, whoAmI);
						break;
					case OutlandCombatTechniquesSystem.UseRequestPacket:
						OutlandCombatTechniquesSystem.ReceiveUseRequest(reader, whoAmI);
						break;
					case EnergyShieldPlayer.ShieldStatePacket:
						EnergyShieldPlayer.ReceiveShieldState(reader, whoAmI);
						break;
					case CultistSummonSystem.SummonRequestPacket:
						CultistSummonSystem.ReceiveSummonRequest(reader, whoAmI);
						break;
					case TheSonSummonSystem.SummonRequestPacket:
						TheSonSummonSystem.ReceiveSummonRequest(reader, whoAmI);
						break;
					case WallofShadowArenaSystem.RequestPacket:
						WallofShadowArenaSystem.ReceiveRequest(whoAmI);
						break;
					case WallofShadowArenaSystem.StatePacket:
						WallofShadowArenaSystem.ReceiveState(reader);
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
					case TerraShieldPlayer.StatePacket:
						TerraShieldPlayer.ReceiveState(reader, whoAmI);
						break;
					case TerraWingsPlayer.DashVisualPacket:
						TerraWingsPlayer.ReceiveDashVisual(reader, whoAmI);
						break;
					case CompositeAccessoryPlayer.DashVisualPacket:
						CompositeAccessoryPlayer.ReceiveDashVisual(reader, whoAmI);
						break;
					case SmartScopePlayer.RequestPacket:
						SmartScopePlayer.ReceiveRequest(reader, whoAmI);
						break;
					case SmartScopePlayer.StatePacket:
						SmartScopePlayer.ReceiveState(reader);
						break;
					case AntaresArmorPlayer.GravityWellRequestPacket:
						AntaresArmorPlayer.ReceiveGravityWellRequest(reader, whoAmI);
						break;
					case AntaresArmorPlayer.GravityWellActivatedPacket:
						AntaresArmorPlayer.ReceiveGravityWellActivated();
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
