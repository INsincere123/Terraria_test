using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC
    {
        internal const byte GuideItemPacket = 7;
        internal enum GuideAction : byte { Open, Help, Reset, Close, CoreReward, DisruptorReward }

        public override void GetChat(NPC npc, ref string chat)
        {
            if (npc.type != NPCID.Guide || Main.netMode == NetmodeID.Server) return;
            SendGuideAction(npc, GuideAction.Open);
        }

        public override bool PreChatButtonClicked(NPC npc, bool firstButton)
        {
            if (npc.type != NPCID.Guide || Main.netMode == NetmodeID.Server) return true;
            return !SendGuideAction(npc, firstButton ? GuideAction.Help : GuideAction.Reset);
        }

        private bool SendGuideAction(NPC npc, GuideAction action)
        {
            Player player = Main.LocalPlayer;
            if (!player.TryGetModPlayer(out TimeEchoPlayer echo)) return false;
            if (action == GuideAction.Open) echo.BeginGuideChat(npc.whoAmI);
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = Mod.GetPacket();
                packet.Write(GuideItemPacket);
                packet.Write((byte)action);
                packet.Write((short)npc.whoAmI);
                packet.Write(echo.GuideChatSerial);
                packet.Send();
                // 默认提示立即显示；特殊对话在服务器确认奖励后覆盖。
                return false;
            }
            GuideAction? reward = echo.ProcessGuideAction(action, npc.whoAmI, echo.GuideChatSerial);
            if (!reward.HasValue) return false;
            ShowGuideReward(reward.Value);
            return true;
        }

        private static void ShowGuideReward(GuideAction reward)
        {
            string key = reward == GuideAction.CoreReward ? "CoreReward" : "DisruptorReward";
            Main.npcChatText = Language.GetTextValue("Mods.TestMod.GuideTimeEcho." + key);
        }

        internal static void ReceiveGuideItem(BinaryReader reader, int sender)
        {
            GuideAction action = (GuideAction)reader.ReadByte();
            int npcIndex = reader.ReadInt16();
            uint serial = reader.ReadUInt32();
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                if (action != GuideAction.CoreReward && action != GuideAction.DisruptorReward) return;
                Player local = Main.LocalPlayer;
                if (local.TryGetModPlayer(out TimeEchoPlayer echo) && !local.dead &&
                    local.talkNPC == npcIndex && !Main.InGuideCraftMenu && echo.GuideChatSerial == serial)
                    ShowGuideReward(action);
                return;
            }
            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers) return;
            if (action != GuideAction.Open && action != GuideAction.Help &&
                action != GuideAction.Reset && action != GuideAction.Close) return;
            Player player = Main.player[sender];
            if (!player.active || !player.TryGetModPlayer(out TimeEchoPlayer state)) return;
            GuideAction? reward = state.ProcessGuideAction(action, npcIndex, serial);
            if (!reward.HasValue) return;
            ModPacket packet = state.Mod.GetPacket();
            packet.Write(GuideItemPacket);
            packet.Write((byte)reward.Value);
            packet.Write((short)npcIndex);
            packet.Write(serial);
            packet.Send(sender);
        }
    }
}
