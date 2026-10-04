using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Tiles;

namespace TestMod.Common.Systems
{
    public partial class ArenaAltarSystem
    {
        private const int DiscoveryChunkSize = 64;
        private const int DiscoveryRadiusTiles = (int)AltarRangeTiles + 10;

        // 空区域只保存已扫描标记；有祭坛的区域才分配坐标集合。
        private static readonly HashSet<Point> _scannedChunks = new();
        private static readonly Dictionary<Point, HashSet<Point>> _chunkAltars = new();
        private static readonly HashSet<Point> _requiredChunks = new();
        private static readonly List<Point> _invalidAltars = new();

        /// <summary>由放置和 TileFrame 调用，按家具帧偏移登记左上角。</summary>
        internal static void ObserveAltarTile(int i, int j)
        {
            if (!IsTileAvailable(i, j)) return;
            Tile tile = Main.tile[i, j];
            if (!tile.HasTile || tile.TileType != ModContent.TileType<HuangQuanAltarTile>()) return;

            Point origin = new(i - tile.TileFrameX / 18 % 2, j - tile.TileFrameY / 18 % 3);
            if (!IsAltarOrigin(origin)) return;

            RegisterAltar(origin);
            if (IsInPlayerDiscoveryRange(origin))
                _altarPositions.Add(origin);
        }

        internal static void ForgetAltar(int i, int j)
        {
            Point origin = new(i, j);
            Point chunk = ChunkOf(origin);
            if (_chunkAltars.TryGetValue(chunk, out HashSet<Point> positions))
            {
                positions.Remove(origin);
                if (positions.Count == 0)
                    _chunkAltars.Remove(chunk);
            }
            _altarPositions.Remove(origin);
            _activeAltarPositions.Remove(origin);
        }

        private static Point ChunkOf(Point tile) => new(tile.X / DiscoveryChunkSize, tile.Y / DiscoveryChunkSize);

        private static bool IsTileAvailable(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY
                && (Main.netMode != NetmodeID.MultiplayerClient || Main.sectionManager?.TileLoaded(x, y) == true);
        }

        private static bool IsAltarOrigin(Point origin)
        {
            if (!IsTileAvailable(origin.X, origin.Y)) return false;
            Tile tile = Main.tile[origin.X, origin.Y];
            return tile.HasTile && tile.TileType == ModContent.TileType<HuangQuanAltarTile>()
                && tile.TileFrameX == 0 && tile.TileFrameY == 0;
        }

        private static void RegisterAltar(Point origin)
        {
            Point chunk = ChunkOf(origin);
            if (!_chunkAltars.TryGetValue(chunk, out HashSet<Point> positions))
            {
                positions = new HashSet<Point>();
                _chunkAltars.Add(chunk, positions);
            }
            positions.Add(origin);
        }

        private static bool IsInPlayerDiscoveryRange(Point origin)
        {
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (!player.active) continue;
                int cx = (int)(player.Center.X / 16);
                int cy = (int)(player.Center.Y / 16);
                if (Math.Abs(origin.X - cx) <= DiscoveryRadiusTiles && Math.Abs(origin.Y - cy) <= DiscoveryRadiusTiles)
                    return true;
            }
            return false;
        }

        private static void RefreshNearbyAltarPositions()
        {
            _altarPositions.Clear();
            _requiredChunks.Clear();
            _invalidAltars.Clear();

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (!player.active) continue;
                int cx = (int)(player.Center.X / 16);
                int cy = (int)(player.Center.Y / 16);
                int minChunkX = Math.Max(0, cx - DiscoveryRadiusTiles) / DiscoveryChunkSize;
                int maxChunkX = Math.Min(Main.maxTilesX - 1, cx + DiscoveryRadiusTiles) / DiscoveryChunkSize;
                int minChunkY = Math.Max(0, cy - DiscoveryRadiusTiles) / DiscoveryChunkSize;
                int maxChunkY = Math.Min(Main.maxTilesY - 1, cy + DiscoveryRadiusTiles) / DiscoveryChunkSize;

                for (int x = minChunkX; x <= maxChunkX; x++)
                for (int y = minChunkY; y <= maxChunkY; y++)
                    _requiredChunks.Add(new Point(x, y));
            }

            foreach (Point chunk in _requiredChunks)
            {
                DiscoverChunk(chunk);
                if (!_chunkAltars.TryGetValue(chunk, out HashSet<Point> positions)) continue;
                foreach (Point origin in positions)
                {
                    // 尚未接收的地图不是空地，不据此删除登记。
                    if (!IsTileAvailable(origin.X, origin.Y)) continue;
                    if (!IsAltarOrigin(origin))
                        _invalidAltars.Add(origin);
                    else if (IsInPlayerDiscoveryRange(origin))
                        _altarPositions.Add(origin);
                }
            }

            foreach (Point origin in _invalidAltars)
                ForgetAltar(origin.X, origin.Y);
        }

        private static void DiscoverChunk(Point chunk)
        {
            int minX = chunk.X * DiscoveryChunkSize;
            int minY = chunk.Y * DiscoveryChunkSize;
            int maxX = Math.Min(Main.maxTilesX - 1, minX + DiscoveryChunkSize - 1);
            int maxY = Math.Min(Main.maxTilesY - 1, minY + DiscoveryChunkSize - 1);
            bool topLeft = IsTileAvailable(minX, minY);
            bool topRight = IsTileAvailable(maxX, minY);
            bool bottomLeft = IsTileAvailable(minX, maxY);
            bool bottomRight = IsTileAvailable(maxX, maxY);
            bool fullyLoaded = topLeft && topRight && bottomLeft && bottomRight;

            if (fullyLoaded && _scannedChunks.Contains(chunk)) return;
            if (!fullyLoaded) _scannedChunks.Remove(chunk);
            // 64 格区域最多跨越相邻的地图 section，四角均未加载时无需遍历。
            if (!topLeft && !topRight && !bottomLeft && !bottomRight) return;

            int altarType = ModContent.TileType<HuangQuanAltarTile>();
            for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            {
                if (!fullyLoaded && !IsTileAvailable(x, y)) continue;
                Tile tile = Main.tile[x, y];
                if (tile.HasTile && tile.TileType == altarType && tile.TileFrameX == 0 && tile.TileFrameY == 0)
                    RegisterAltar(new Point(x, y));
            }

            // 只缓存完整收到的区域，旧世界和稍后到达的网络 section 都会被发现。
            if (fullyLoaded) _scannedChunks.Add(chunk);
        }

        private static void ClearDiscoveryCache()
        {
            _scannedChunks.Clear();
            _chunkAltars.Clear();
            _requiredChunks.Clear();
            _invalidAltars.Clear();
        }
    }
}
