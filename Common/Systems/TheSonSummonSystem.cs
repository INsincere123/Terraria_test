using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Content.Items.BossSummons;

namespace TestMod.Common.Systems
{
    public sealed class TheSonSummonSystem : ModSystem
    {
        internal const byte SummonRequestPacket = 17;
        private static int _bossType;
        private static Type _bossRuntimeType;
        private static FieldInfo _lockPosition;
        internal static bool IsReady { get; private set; }

        public override void PostSetupContent()
        {
            ClearCompatibility();
            if (!ModLoader.TryGetMod("ContinentOfJourney", out Mod journey))
                return;

            try
            {
                if (!journey.TryFind<ModNPC>("TheSon", out var boss))
                    throw new InvalidOperationException("找不到圣子 NPC。");

                Type type = boss.GetType();
                FieldInfo field = type.GetField("lockPos", BindingFlags.Instance | BindingFlags.Public);
                if (field == null || field.FieldType != typeof(Vector2) || field.IsInitOnly)
                    throw new MissingFieldException(type.FullName, "lockPos: Vector2");
                // 当前安装版 0.8.71.17 已核对：普通/大师开场都在定位祭坛后写入
                // lockPos、ai[1] = 1，且通过这两个原有入口同步 lockPos。
                if (type.GetMethod(nameof(ModNPC.SendExtraAI), new[] { typeof(BinaryWriter) })?.DeclaringType != type ||
                    type.GetMethod(nameof(ModNPC.ReceiveExtraAI), new[] { typeof(BinaryReader) })?.DeclaringType != type)
                    throw new MissingMethodException(type.FullName, "SendExtraAI/ReceiveExtraAI");

                _bossType = boss.Type;
                _bossRuntimeType = type;
                _lockPosition = field;
                IsReady = true;
            }
            catch (Exception exception)
            {
                ClearCompatibility();
                Mod.Logger.Warn($"[TheSonSummon] 开场初始化适配不可用，召唤物停用：{exception}");
            }
        }

        public override void Unload() => ClearCompatibility();

        private static void ClearCompatibility()
        {
            IsReady = false;
            _bossType = 0;
            _bossRuntimeType = null;
            _lockPosition = null;
        }

        internal static bool CanSummon(Player player)
            => IsReady && player.active && !player.dead &&
                float.IsFinite(player.Center.X) && float.IsFinite(player.Center.Y) && !NPC.AnyNPCs(_bossType);

        internal static void RequestSummon(Player player)
        {
            if (player.whoAmI != Main.myPlayer || !CanSummon(player))
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // 不接受客户端指定位置或玩家编号，服务器从发送者当前状态重建。
                ModPacket packet = ModContent.GetInstance<TheSonSummonSystem>().Mod.GetPacket();
                packet.Write(SummonRequestPacket);
                packet.Send();
            }
            else
                TrySummon(player);
        }

        internal static void ReceiveSummonRequest(BinaryReader reader, int sender)
        {
            if (Main.netMode == NetmodeID.Server && sender >= 0 && sender < Main.maxPlayers)
                TrySummon(Main.player[sender]);
        }

        internal static bool TrySummon(Player player)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !CanSummon(player) ||
                player.HeldItem.ModItem is not TheSonSummon)
                return false;

            // 将玩家脚下对应到原 AI 的 lockPos + 176 平台高度，不创建或改动物块。
            Vector2 center = player.Bottom - new Vector2(0f, 176f);
            center.X = MathHelper.Clamp(center.X, 160f, Main.maxTilesX * 16f - 160f);
            center.Y = MathHelper.Clamp(center.Y, 160f, Main.maxTilesY * 16f - 160f);
            int index = NPC.NewNPC(player.GetSource_ItemUse(player.HeldItem),
                (int)center.X, (int)center.Y, _bossType, Target: player.whoAmI);
            if (index < 0 || index >= Main.maxNPCs)
                return false;

            NPC npc = Main.npc[index];
            try
            {
                if (npc.ModNPC?.GetType() != _bossRuntimeType)
                    throw new InvalidOperationException("圣子实例类型与加载模板不一致。");

                // 仅初始化本道具新建的实例。普通模式跳过 case 0 的祭坛扫描，
                // 大师模式跳过 lockPos.X <= 0 的扫描；两者均从原开场计时起点继续。
                npc.Center = center;
                npc.velocity = Vector2.Zero;
                _lockPosition.SetValue(npc.ModNPC, center);
                npc.ai[1] = 1f;
                npc.netUpdate = true;
                npc.netSpam = 0;
            }
            catch (Exception exception)
            {
                npc.active = false;
                ClearCompatibility();
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: index);
                ModContent.GetInstance<TheSonSummonSystem>().Mod.Logger.Warn(
                    $"[TheSonSummon] 开场初始化失败，召唤物停用：{exception}");
                return false;
            }

            if (Main.netMode == NetmodeID.Server)
            {
                // 位置、ai[] 与原模组 SendExtraAI 中的 lockPos 在第一次同步前已就绪。
                NetMessage.SendData(MessageID.SyncNPC, number: index);
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasAwoken", npc.TypeName),
                    new Color(175, 75, 255));
            }
            else
                Main.NewText(NetworkText.FromKey("Announcement.HasAwoken", npc.TypeName).ToString(), 175, 75, 255);
            return true;
        }
    }
}
