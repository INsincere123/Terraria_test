using System;
using System.IO;
using System.Linq;
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
    public sealed partial class WallofShadowArenaSystem : ModSystem
    {
        internal const byte RequestPacket = 18, StatePacket = 19;
        internal const int Width = 74, Height = 44;
        private static int _bossType, _insideType, _borderType, _doorType, _wallType, _horrified, _markerType;
        private static int[,] _template;
        private static bool _ready, _recoveryFailed;
        private static Snapshot _active;
        private static NPC _boss;
        private static readonly bool[] Participants = new bool[256];
        internal static Rectangle ProtectedArea { get; private set; }
        internal static Guid Session => _active?.Id ?? Guid.Empty;
        internal static bool Protects(int x, int y) => ProtectedArea.Contains(x, y);

        public override void PostSetupContent()
        {
            _ready = false;
            _template = null;
            _markerType = ModContent.TileType<WallArenaMarkerTile>();
            if (!ModLoader.TryGetMod("ContinentOfJourney", out Mod journey)) return;
            try
            {
                _bossType = journey.Find<ModNPC>("WallofShadow").Type;
                _insideType = journey.Find<ModTile>("TrueAbyssBrick_2").Type;
                _borderType = journey.Find<ModTile>("TrueAbyssBrick").Type;
                _doorType = journey.Find<ModTile>("TrueAbyssBrick_3").Type;
                _wallType = journey.Find<ModWall>("AbyssBrickWall").Type;
                _horrified = journey.Find<ModBuff>("Horrified").Type;
                // 仅读取已安装版的模板，不执行世界生成方法；安装版0.8.71.17已核对。
                Type type = journey.Code.GetType("ContinentOfJourney.ShadowWallPass", true);
                _template = ReadArenaTemplate(type);
                _ready = true;
            }
            catch (Exception ex)
            {
                _template = null;
                Mod.Logger.Warn($"[WallofShadowArena] 场地适配不可用，召唤物停用：{ex}");
            }
        }

        private static int[,] ReadArenaTemplate(Type type)
        {
            // 0.8.71.17自身签名为(string,float)，其IL中的double是对GenPass基类的调用。
            ConstructorInfo constructor = type.GetConstructor(new[] { typeof(string), typeof(float) })
                ?? type.GetConstructor(new[] { typeof(string), typeof(double) })
                ?? throw new MissingMethodException(type.FullName, ".ctor(string,float/double)");
            object weight = 0f;
            if (constructor.GetParameters()[1].ParameterType == typeof(double)) weight = 0d;
            object pass = constructor.Invoke(new object[] { "TestMod arena template", weight });
            var template = type.GetField("_house2", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(pass) as int[,];
            if (template == null || template.GetLength(0) != Height || template.GetLength(1) != Width)
                throw new InvalidOperationException("封印模板尺寸不匹配。");
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (template[y, x] is < 0 or > 2 ||
                        (template[y, x] == 0) != (x >= 2 && x < Width - 2 && y >= 2 && y < Height - 2))
                        throw new InvalidOperationException("封印模板边界或内部不匹配。");
            return template;
        }

        internal static bool CanRequest(Player player) => RequestFailure(player) == null;

        private static string RequestFailure(Player player)
        {
            if (!player.active || player.dead) return "CheckPlayer";
            if (!_ready) return "CheckNotReady";
            if (_recoveryFailed) return "CheckRecovery";
            if (Journal.Count >= 4096) return "CheckLimit";
            if (_active != null || !ProtectedArea.IsEmpty) return "CheckActive";
            if (!float.IsFinite(player.Center.X) || !float.IsFinite(player.Center.Y)) return "CheckPosition";
            if (NPC.AnyNPCs(_bossType)) return "CheckSameBoss";
            return null;
        }

        internal static void Request(Player player)
        {
            if (player.whoAmI != Main.myPlayer || !player.active || player.dead) return;
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                Tell(player, "CheckWaiting");
                ModPacket packet = ModContent.GetInstance<WallofShadowArenaSystem>().Mod.GetPacket();
                packet.Write(RequestPacket);
                packet.Send();
            }
            else TryCreate(player);
        }

        internal static void ReceiveRequest(int sender)
        {
            if (Main.netMode == NetmodeID.Server && sender >= 0 && sender < Main.maxPlayers)
                TryCreate(Main.player[sender]);
        }

        private static void TryCreate(Player player)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !player.active || player.dead) return;
            if (player.HeldItem.ModItem is not WallofShadowSummon)
            {
                Tell(player, "CheckHeldItem");
                return;
            }
            string failure = RequestFailure(player);
            if (failure != null)
            {
                Tell(player, failure);
                return;
            }
            Rectangle area = new((int)(player.Center.X / 16) - Width / 2,
                (int)(player.Center.Y / 16) - Height / 2, Width, Height);
            Tell(player, "CheckStart", area.X, area.Y);
            if (!CheckBuild(area, out string reason, out object[] arguments))
            {
                Tell(player, reason, arguments);
                return;
            }
            Tell(player, "CheckTerrainPassed");
            if (!TryFindExit(area, player, out Vector2 exit))
            {
                Tell(player, "CheckNoExit");
                return;
            }
            Tell(player, "CheckExitPassed", (int)(exit.X / 16), (int)(exit.Y / 16));
            try
            {
                Snapshot snapshot = Snapshot.Capture(area, exit);
                // 磁盘日志必须在任何改地形前落盘；保存世界时也写入twld。
                lock (Journal)
                {
                    Journal.Add(snapshot);
                    WriteJournal();
                }
                _active = snapshot;
                ProtectedArea = area;
                Array.Clear(Participants);
                Build(area);
                TrackParticipants();
                SendState(true);
                SyncTiles(area);
                int index = NPC.NewNPC(player.GetSource_ItemUse(player.HeldItem),
                    (area.X + 2) * 16, (area.Y + 2) * 16, _bossType, Target: player.whoAmI);
                if (index < 0 || index >= Main.maxNPCs) throw new InvalidOperationException("NPC槽位不足。");
                _boss = Main.npc[index];
                _boss.position = new Vector2((area.X + 2) * 16, (area.Y + 2) * 16);
                // 此位置正是原AI扫描得到的左上内部角；保留眼睛生成及所有后续AI。
                _boss.localAI[0] = 5;
                _boss.GetGlobalNPC<WallArenaNPC>().SessionId = snapshot.Id;
                _boss.netUpdate = true;
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendData(MessageID.SyncNPC, number: index);
                    ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasAwoken", _boss.TypeName), new Color(175, 75, 255));
                }
                else Main.NewText(NetworkText.FromKey("Announcement.HasAwoken", _boss.TypeName).ToString(), 175, 75, 255);
                Tell(player, "CheckCreated");
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<WallofShadowArenaSystem>().Mod.Logger.Warn($"[WallofShadowArena] 创建失败：{ex}");
                if (_active != null) Finish();
                else _recoveryFailed = true;
                Tell(player, "Unavailable");
            }
        }

        public override void PostUpdateNPCs()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || _active == null || _recoveryFailed) return;
            TrackParticipants();
            if (_boss == null || !_boss.active || _boss.type != _bossType ||
                !ReferenceEquals(Main.npc[_boss.whoAmI], _boss)) Finish();
        }

        private static void TrackParticipants()
        {
            Rectangle pixels = PixelArea(ProtectedArea);
            foreach (Player player in Main.player)
                if (player.active && pixels.Intersects(player.Hitbox)) Participants[player.whoAmI] = true;
        }

        private static void Finish()
        {
            Snapshot snapshot = _active;
            if (snapshot == null) return;
            try
            {
                TrackParticipants();
                RefreshExit(snapshot);
                CleanupEntities(snapshot.Id);
                // 先通知客户端移除本场惊恐并撤离，再同步恢复地形，避免恢复时误杀。
                SendState(false);
                foreach (Player player in Main.player)
                    if (player.active && (Participants[player.whoAmI] || PixelArea(snapshot.Area).Intersects(player.Hitbox)))
                        Evacuate(player, snapshot.Exit, PixelArea(snapshot.Area).Intersects(player.Hitbox));
                // 原地恢复可能把战利品或原有地面物品埋入实心地形，保留数量/身份并移到出口。
                for (int i = 0; i < Main.maxItems; i++)
                    if (Main.item[i].active && PixelArea(snapshot.Area).Intersects(Main.item[i].Hitbox))
                    {
                        Main.item[i].position = snapshot.Exit;
                        Main.item[i].velocity = Vector2.Zero;
                        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncItem, number: i);
                    }
                snapshot.Restore();
                SyncTiles(snapshot.Area);
                Recovered.Add(snapshot);
                _active = null;
                _boss = null;
                ProtectedArea = default;
                Array.Clear(Participants);
            }
            catch (Exception ex)
            {
                _recoveryFailed = true;
                ModContent.GetInstance<WallofShadowArenaSystem>().Mod.Logger.Error($"[WallofShadowArena] 恢复失败，保留快照与日志：{ex}");
            }
        }

        public override void PreSaveAndQuit()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) Finish();
        }

        private static Rectangle PixelArea(Rectangle area) => new(area.X * 16, area.Y * 16, area.Width * 16, area.Height * 16);
        private static void RefreshExit(Snapshot snapshot)
        {
            Player player = Main.player.FirstOrDefault(p => p.active && !p.dead);
            if (player != null && TryFindExit(snapshot.Area, player, out Vector2 exit)) snapshot.Exit = exit;
        }
        private static void Evacuate(Player player, Vector2 exit, bool move)
        {
            if (_horrified > 0) player.ClearBuff(_horrified);
            if (move && !player.dead)
            {
                player.Teleport(exit, 1);
                player.velocity = Vector2.Zero;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.TeleportEntity, number: 0, number2: player.whoAmI,
                        number3: exit.X, number4: exit.Y, number5: 1);
            }
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.PlayerBuffs, number: player.whoAmI);
        }

        private static void Tell(Player player, string key, params object[] arguments)
        {
            string fullKey = "Mods.TestMod.Items.WallofShadowSummon." + key;
            if (Main.netMode == NetmodeID.Server)
                ChatHelper.SendChatMessageToClient(NetworkText.FromKey(fullKey, arguments), Color.Orange, player.whoAmI);
            else Main.NewText(Language.GetTextValue(fullKey, arguments), Color.Orange);
        }

        private static void SendState(bool active)
        {
            if (Main.netMode != NetmodeID.Server) return;
            ModPacket packet = ModContent.GetInstance<WallofShadowArenaSystem>().Mod.GetPacket();
            packet.Write(StatePacket);
            packet.Write(active);
            WriteArea(packet, _active.Area);
            packet.Write(_active.Exit.X); packet.Write(_active.Exit.Y);
            for (int i = 0; i < Main.maxPlayers; i++) packet.Write(Participants[i]);
            packet.Send();
        }

        internal static void ReceiveState(BinaryReader reader)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            bool active = reader.ReadBoolean();
            Rectangle area = ReadArea(reader);
            Vector2 exit = new(reader.ReadSingle(), reader.ReadSingle());
            bool localParticipant = false;
            for (int i = 0; i < Main.maxPlayers; i++)
                if (reader.ReadBoolean() && i == Main.myPlayer) localParticipant = true;
            if (!ValidArea(area) || !float.IsFinite(exit.X) || !float.IsFinite(exit.Y)) return;
            ProtectedArea = active ? area : default;
            if (!active && (localParticipant || PixelArea(area).Intersects(Main.LocalPlayer.Hitbox)))
                Evacuate(Main.LocalPlayer, exit, PixelArea(area).Intersects(Main.LocalPlayer.Hitbox));
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(!ProtectedArea.IsEmpty);
            if (!ProtectedArea.IsEmpty) WriteArea(writer, ProtectedArea);
            writer.Write(Recovered.Count);
            foreach (Snapshot snapshot in Recovered)
            {
                WriteArea(writer, snapshot.Area);
                writer.Write(snapshot.Exit.X); writer.Write(snapshot.Exit.Y);
            }
        }
        public override void NetReceive(BinaryReader reader)
        {
            ProtectedArea = reader.ReadBoolean() ? ReadArea(reader) : default;
            if (!ProtectedArea.IsEmpty && !ValidArea(ProtectedArea)) throw new InvalidDataException("Invalid arena bounds");
            Recovered.Clear();
            int count = reader.ReadInt32();
            if (count is < 0 or > 4096) throw new InvalidDataException("Invalid recovery count");
            for (int i = 0; i < count; i++)
            {
                Rectangle area = ReadArea(reader);
                Vector2 exit = new(reader.ReadSingle(), reader.ReadSingle());
                if (ValidArea(area) && float.IsFinite(exit.X) && float.IsFinite(exit.Y)) Recovered.Add(new Snapshot { Area = area, Exit = exit });
            }
        }
        private static void WriteArea(BinaryWriter w, Rectangle r) { w.Write(r.X); w.Write(r.Y); }
        private static Rectangle ReadArea(BinaryReader r) => new(r.ReadInt32(), r.ReadInt32(), Width, Height);
    }
}
