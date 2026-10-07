using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Content.Items.Tools;

namespace TestMod.Common.GlobalNPCs
{
    // 时之回响两个消耗品的隐藏获取途径：通过向导对话。
    public partial class GlobalNPC
    {
        private const int RequiredHelpClicks = 10;
        private const int DisruptorOdds = 6; // 每次帮助点击 1/6 概率触发断裂器
        internal const byte GuideItemPacket = 7;

        // 纯客户端 UI 状态：每个玩家在当前向导对话中的连续帮助点击次数。
        private static int[] _guideHelpStreak = new int[Main.maxPlayers];

        public override void GetChat(NPC npc, ref string chat)
        {
            if (npc.type != NPCID.Guide) return;
            if (Main.netMode == NetmodeID.Server) return;
            // 每次开启一次新的向导对话，连续帮助计数清零。
            _guideHelpStreak[Main.myPlayer] = 0;
        }

        // 注意：帮助按钮的默认动作（HelpText 写入随机提示）在本钩子之后执行，
        // 因此触发时必须返回 false 阻止它覆盖我们的对话文本。
        public override bool PreChatButtonClicked(NPC npc, bool firstButton)
        {
            if (npc.type != NPCID.Guide) return true;

            // 第二个按钮（合成）不是帮助，打断连续计数。
            if (!firstButton)
            {
                _guideHelpStreak[Main.myPlayer] = 0;
                return true;
            }

            Player player = Main.player[Main.myPlayer];
            TimeEchoPlayer echo = player.GetModPlayer<TimeEchoPlayer>();

            // ---- 时之回响核心：满月 + 无此物品 + 无能力 + 连续帮助 10 次 ----
            if (!echo.HasAbility)
            {
                int coreType = ModContent.ItemType<TimeEchoCore>();
                bool coreEligible = Main.moonPhase == 0 && !player.HasItem(coreType);
                if (!coreEligible)
                {
                    // 非满月或已有物品时帮助点击不计入连续次数。
                    _guideHelpStreak[player.whoAmI] = 0;
                    return true;
                }
                int streak = ++_guideHelpStreak[player.whoAmI];
                if (streak >= RequiredHelpClicks)
                {
                    _guideHelpStreak[player.whoAmI] = 0;
                    Main.npcChatText = Language.GetTextValue("Mods.TestMod.GuideTimeEcho.CoreReward");
                    RequestGuideItem(player, coreType);
                    return false;
                }
                return true;
            }

            // ---- 时之断裂器：拥有时之幻影能力后，帮助点击随机触发 ----
            _guideHelpStreak[player.whoAmI] = 0;
            int disruptorType = ModContent.ItemType<TimeEchoDisruptor>();
            if (!player.HasItem(disruptorType) && Main.rand.NextBool(DisruptorOdds))
            {
                Main.npcChatText = Language.GetTextValue("Mods.TestMod.GuideTimeEcho.DisruptorReward");
                RequestGuideItem(player, disruptorType);
                return false;
            }
            return true;
        }

        // 客户端触发：单机直接生成；多人客户端请求服务器生成（物品生成必须服务器权威）。
        private void RequestGuideItem(Player player, int itemType)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                player.QuickSpawnItem(player.GetSource_GiftOrReward(), itemType);
            }
            else if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = Mod.GetPacket();
                packet.Write(GuideItemPacket);
                packet.Write(itemType);
                packet.Send();
            }
        }

        // 服务器端：不信任客户端的连续计数，只重新校验客观条件后生成物品。
        internal static void ReceiveGuideItem(BinaryReader reader, int sender)
        {
            if (Main.netMode != NetmodeID.Server) return;
            if (sender < 0 || sender >= Main.maxPlayers) return;
            Player player = Main.player[sender];
            if (!player.active) return;

            int itemType = reader.ReadInt32();
            TimeEchoPlayer echo = player.GetModPlayer<TimeEchoPlayer>();

            if (itemType == ModContent.ItemType<TimeEchoCore>())
            {
                if (Main.moonPhase != 0 || player.HasItem(itemType) || echo.HasAbility) return;
                player.QuickSpawnItem(player.GetSource_GiftOrReward(), itemType);
            }
            else if (itemType == ModContent.ItemType<TimeEchoDisruptor>())
            {
                if (!echo.HasAbility || player.HasItem(itemType)) return;
                player.QuickSpawnItem(player.GetSource_GiftOrReward(), itemType);
            }
        }
    }
}