using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TestMod.Common.Systems
{
    public sealed partial class WallofShadowArenaSystem
    {
        private static readonly List<Snapshot> Journal = new();
        private static readonly List<Snapshot> Recovered = new();
        private static TagCompound _unparsedData;
        private static string JournalPath => Path.Combine(Main.SavePath, "TestMod", "WallArenaRecovery",
            Main.ActiveWorldFileData.UniqueId.ToString("N") + ".dat");

        public override void ClearWorld()
        {
            Journal.Clear(); Recovered.Clear(); _unparsedData = null;
            _active = null; _boss = null; ProtectedArea = default; _recoveryFailed = false;
            Array.Clear(Participants);
        }

        private static TagCompound SaveJournal()
        {
            lock (Journal)
                return new TagCompound
                {
                    ["schema"] = 1, ["world"] = Main.ActiveWorldFileData.UniqueId.ToString(),
                    ["width"] = Main.maxTilesX, ["height"] = Main.maxTilesY,
                    ["snapshots"] = Journal.Select(snapshot => snapshot.Save()).ToList()
                };
        }

        private static void MergeJournal(TagCompound root)
        {
            if (root.GetInt("schema") != 1 || root.GetString("world") != Main.ActiveWorldFileData.UniqueId.ToString() ||
                root.GetInt("width") != Main.maxTilesX || root.GetInt("height") != Main.maxTilesY)
                throw new InvalidDataException("恢复日志世界标识或尺寸不匹配。");
            // 整批校验完成后再接纳，不能在损坏快照上开始恢复地形。
            Snapshot[] snapshots = root.GetList<TagCompound>("snapshots").Select(Snapshot.Load).ToArray();
            if (snapshots.Length > 4096) throw new InvalidDataException("恢复日志过大。");
            foreach (Snapshot snapshot in snapshots)
                if (Journal.All(existing => existing.Id != snapshot.Id)) Journal.Add(snapshot);
        }

        private static void WriteJournal()
        {
            string path = JournalPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                TagIO.ToStream(SaveJournal(), stream);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (_unparsedData != null) tag["recovery"] = _unparsedData;
            else if (Journal.Count > 0) tag["recovery"] = SaveJournal();
        }

        public override void LoadWorldData(TagCompound tag)
        {
            if (!tag.ContainsKey("recovery")) return;
            try { MergeJournal(tag.GetCompound("recovery")); }
            catch (Exception ex)
            {
                _unparsedData = tag.GetCompound("recovery");
                _recoveryFailed = true;
                Mod.Logger.Error($"[WallofShadowArena] 世界快照无法解析，保留原数据并禁止新场地：{ex}");
            }
        }

        public override void PostWorldLoad()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            try
            {
                if (File.Exists(JournalPath)) MergeJournal(TagIO.FromFile(JournalPath));
                if (_recoveryFailed) return;
                // 新场地快照优先；同一地点反复召唤也不会将较旧的地形盖回去。
                foreach (Snapshot snapshot in Journal.OrderByDescending(s => s.Created))
                {
                    Tile marker = Main.tile[snapshot.Area.X, snapshot.Area.Y];
                    if (!marker.HasTile || marker.TileType != _markerType) continue;
                    snapshot.Restore();
                    Recovered.Add(snapshot);
                    Mod.Logger.Info($"[WallofShadowArena] 已恢复未清理场地：{snapshot.Area}");
                }
                // 只有从磁盘加载的世界已无临时标记时才删除日志。
                // 刚在内存恢复过的记录保留，直到下次加载证明恢复已随世界落盘。
                if (Recovered.Count == 0)
                {
                    Journal.Clear();
                    if (File.Exists(JournalPath)) File.Delete(JournalPath);
                }
            }
            catch (Exception ex)
            {
                _recoveryFailed = true;
                Mod.Logger.Error($"[WallofShadowArena] 自动恢复失败，日志保留且禁止新场地：{ex}");
            }
        }

        public override void PreUpdatePlayers()
        {
            // 中途存档的玩家可能保存于场内；恢复地形后在首次Buff更新前撤离，防卡墙/惊恐误杀。
            foreach (Player player in Main.player)
            {
                if (!player.active || player.dead || (Main.netMode == NetmodeID.MultiplayerClient && player.whoAmI != Main.myPlayer)) continue;
                foreach (Snapshot snapshot in Recovered)
                    if (PixelArea(snapshot.Area).Intersects(player.Hitbox) &&
                        (Collision.SolidCollision(player.position, player.width, player.height) || (_horrified > 0 && player.HasBuff(_horrified))))
                    {
                        RefreshExit(snapshot);
                        Evacuate(player, snapshot.Exit, true);
                        break;
                    }
            }
        }

        public override void OnWorldUnload() => ClearWorld();
        public override void Unload()
        {
            ClearWorld();
            _ready = false; _template = null;
            _bossType = _insideType = _borderType = _doorType = _wallType = _horrified = 0;
        }
    }
}
