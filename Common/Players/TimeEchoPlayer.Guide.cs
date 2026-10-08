using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.Tools;
using GuideAction = TestMod.Common.GlobalNPCs.GlobalNPC.GuideAction;

namespace TestMod.Common.Players
{
    public sealed partial class TimeEchoPlayer
    {
        private int guideNpc = -1;
        private int guideHelpStreak;
        private ulong lastGuideHelpTick = ulong.MaxValue;
        internal uint GuideChatSerial { get; private set; }
        private Item pendingGuideCore, pendingGuideDisruptor;
        private ulong corePickupTick = ulong.MaxValue, disruptorPickupTick = ulong.MaxValue;

        internal void BeginGuideChat(int npcIndex)
        {
            GuideChatSerial++;
            guideNpc = npcIndex;
            guideHelpStreak = 0;
        }

        // talkNPC 是客户端 UI 状态，服务器不靠它证明对话；请求必须属于已开启的会话，
        // 且发送者仍活着、向导仍存在并处于原版保持对话的距离内。
        private bool CanTalkToGuide(int npcIndex)
        {
            if (!Player.active || Player.dead || Player.statLife <= 0 || Player.chest != -1 ||
                npcIndex < 0 || npcIndex >= Main.maxNPCs) return false;
            NPC npc = Main.npc[npcIndex];
            if (!npc.active || npc.type != NPCID.Guide) return false;
            Rectangle reach = new((int)(Player.Center.X - Terraria.Player.tileRangeX * 16),
                (int)(Player.Center.Y - Terraria.Player.tileRangeY * 16),
                Terraria.Player.tileRangeX * 32, Terraria.Player.tileRangeY * 32);
            return reach.Intersects(npc.Hitbox);
        }

        internal void UpdateGuideChat()
        {
            if (guideNpc < 0) return;
            if (!CanTalkToGuide(guideNpc) ||
                (Main.netMode != NetmodeID.Server && Player.talkNPC != guideNpc))
            {
                if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
                {
                    ModPacket packet = Mod.GetPacket();
                    packet.Write(Common.GlobalNPCs.GlobalNPC.GuideItemPacket);
                    packet.Write((byte)GuideAction.Close);
                    packet.Write((short)guideNpc);
                    packet.Write(GuideChatSerial);
                    packet.Send();
                }
                guideNpc = -1;
                guideHelpStreak = 0;
                GuideChatSerial++;
            }
        }

        internal GuideAction? ProcessGuideAction(GuideAction action, int npcIndex, uint serial)
        {
            if (!CanTalkToGuide(npcIndex))
            {
                guideNpc = -1;
                guideHelpStreak = 0;
                return null;
            }
            if (action == GuideAction.Open)
            {
                guideNpc = npcIndex;
                GuideChatSerial = serial;
                guideHelpStreak = 0;
                return null;
            }
            if (guideNpc != npcIndex || GuideChatSerial != serial) return null;
            if (action == GuideAction.Close)
            {
                guideNpc = -1;
                guideHelpStreak = 0;
                return null;
            }
            if (action == GuideAction.Reset)
            {
                guideHelpStreak = 0;
                return null;
            }
            // 单人向导对话可自动暂停，此时 tick 不推进，不能按 tick 丢弃真实 UI 点击。
            if (action != GuideAction.Help ||
                (Main.netMode == NetmodeID.Server && lastGuideHelpTick == Main.GameUpdateCount)) return null;
            lastGuideHelpTick = Main.GameUpdateCount;

            bool core = !HasAbility;
            int itemType = core ? ModContent.ItemType<TimeEchoCore>() : ModContent.ItemType<TimeEchoDisruptor>();
            if ((core && Main.moonPhase != 0) || Player.HasItem(itemType) || HasPendingGuideReward(core))
            {
                guideHelpStreak = 0;
                return null;
            }
            if (core)
            {
                if (++guideHelpStreak < 10) return null;
            }
            else
            {
                guideHelpStreak = 0;
                if (!Main.rand.NextBool(6)) return null;
            }
            guideHelpStreak = 0;
            int index = Player.QuickSpawnItem(Player.GetSource_GiftOrReward(), itemType);
            if (index < 0 || index >= Main.maxItems) return null;
            // 记住生成的实体而非背包状态：背包满或拾取同步未到时不能再次发奖。
            if (core) { pendingGuideCore = Main.item[index]; corePickupTick = ulong.MaxValue; }
            else { pendingGuideDisruptor = Main.item[index]; disruptorPickupTick = ulong.MaxValue; }
            return core ? GuideAction.CoreReward : GuideAction.DisruptorReward;
        }

        private bool HasPendingGuideReward(bool core)
        {
            Item pending = core ? pendingGuideCore : pendingGuideDisruptor;
            if (pending == null) return false;
            int expectedType = core ? ModContent.ItemType<TimeEchoCore>() : ModContent.ItemType<TimeEchoDisruptor>();
            if (pending.active && pending.type == expectedType) return true;
            // 地面实体消失与背包同步并非原子操作，留出同步窗口。
            ref ulong pickupTick = ref (core ? ref corePickupTick : ref disruptorPickupTick);
            if (pickupTick == ulong.MaxValue) pickupTick = Main.GameUpdateCount;
            if (Main.GameUpdateCount - pickupTick < 60) return true;
            if (core) pendingGuideCore = null;
            else pendingGuideDisruptor = null;
            return false;
        }

        private void ResetGuideChat()
        {
            guideNpc = -1;
            guideHelpStreak = 0;
            GuideChatSerial++;
            lastGuideHelpTick = ulong.MaxValue;
            pendingGuideCore = pendingGuideDisruptor = null;
            corePickupTick = disruptorPickupTick = ulong.MaxValue;
        }
    }
}
