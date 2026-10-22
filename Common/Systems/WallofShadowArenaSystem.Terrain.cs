using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TestMod.Common.Systems
{
    public sealed partial class WallofShadowArenaSystem
    {
        // 只允许明确的原版自然地形。拒绝模组方块/背景及住房墙，避免跨加载ID漂移和建筑实体丢失。
        private static readonly HashSet<ushort> NaturalTiles = new()
        {
            TileID.Dirt, TileID.Stone, TileID.Grass, TileID.Mud, TileID.JungleGrass,
            TileID.Sand, TileID.Silt, TileID.ClayBlock, TileID.SnowBlock, TileID.IceBlock,
            TileID.Ash, TileID.Ebonstone, TileID.Crimstone, TileID.Pearlstone,
            TileID.CorruptGrass, TileID.CrimsonGrass, TileID.HallowedGrass,
            TileID.Ebonsand, TileID.Crimsand, TileID.Pearlsand,
            TileID.Copper, TileID.Tin, TileID.Iron, TileID.Lead, TileID.Silver, TileID.Tungsten,
            TileID.Gold, TileID.Platinum, TileID.Cobalt, TileID.Palladium, TileID.Mythril,
            TileID.Orichalcum, TileID.Adamantite, TileID.Titanium, TileID.Chlorophyte
        };

        private static bool ValidArea(Rectangle area) => area.Width == Width && area.Height == Height &&
            area.X >= 10 && area.Y >= 10 && area.X < Main.maxTilesX - Width - 10 && area.Y < Main.maxTilesY - Height - 10;

        private static bool CanBuild(Rectangle area) => CheckBuild(area, out _, out _);

        private static bool CheckBuild(Rectangle area, out string reason, out object[] arguments)
        {
            reason = null;
            arguments = Array.Empty<object>();
            if (!ValidArea(area)) { reason = "CheckBoundary"; return false; }
            Rectangle guard = area; guard.Inflate(3, 3);
            foreach (var pair in TileEntity.ByPosition)
                if (guard.Contains(pair.Key.X, pair.Key.Y))
                { reason = "CheckSpecialObject"; arguments = new object[] { pair.Key.X, pair.Key.Y }; return false; }
            foreach (Chest chest in Main.chest)
                if (chest != null && guard.Contains(chest.x, chest.y))
                { reason = "CheckChest"; arguments = new object[] { chest.x, chest.y }; return false; }
            foreach (Sign sign in Main.sign)
                if (sign != null && guard.Contains(sign.x, sign.y))
                { reason = "CheckSign"; arguments = new object[] { sign.x, sign.y }; return false; }
            foreach (NPC npc in Main.npc)
                if (npc.active && (npc.boss || (npc.townNPC && PixelArea(guard).Intersects(npc.Hitbox))))
                { reason = npc.boss ? "CheckBoss" : "CheckTownNPC"; arguments = new object[] { npc.TypeName }; return false; }
            for (int y = guard.Top; y < guard.Bottom; y++)
                for (int x = guard.Left; x < guard.Right; x++)
                {
                    Tile tile = Main.tile[x, y];
                    string blocked = null;
                    if (tile.TileType >= TileID.Count) blocked = "CheckModTile";
                    else if (tile.WallType >= WallID.Count) blocked = "CheckModWall";
                    else if (tile.HasTile && (!NaturalTiles.Contains(tile.TileType) || Main.tileFrameImportant[tile.TileType])) blocked = "CheckRestrictedTile";
                    else if (tile.WallType > WallID.None && Main.wallHouse[tile.WallType]) blocked = "CheckHousingWall";
                    else if (tile.TileColor != PaintID.None || tile.WallColor != PaintID.None) blocked = "CheckPaint";
                    else if (tile.HasActuator || tile.RedWire || tile.BlueWire || tile.GreenWire || tile.YellowWire) blocked = "CheckWire";
                    else if (tile.IsTileInvisible || tile.IsWallInvisible || tile.IsTileFullbright || tile.IsWallFullbright) blocked = "CheckCoating";
                    // 地形生成不干扰边缘液体/沙块的传播和下落。
                    else if (!area.Contains(x, y) && tile.LiquidAmount != 0) blocked = "CheckBorderLiquid";
                    else if (!area.Contains(x, y) && tile.HasTile && TileID.Sets.Falling[tile.TileType]) blocked = "CheckBorderFalling";
                    if (blocked != null)
                    {
                        reason = blocked;
                        arguments = new object[] { x, y };
                        return false;
                    }
                }
            return true;
        }

        private static bool TryFindExit(Rectangle area, Player player, out Vector2 exit)
        {
            for (int radius = 4; radius <= 40; radius += 4)
                for (int offset = -40; offset <= 40; offset += 4)
                {
                    foreach (Point p in new[] {
                        new Point(area.Center.X + offset, area.Top - radius),
                        new Point(area.Center.X + offset, area.Bottom + radius),
                        new Point(area.Left - radius, area.Center.Y + offset),
                        new Point(area.Right + radius, area.Center.Y + offset) })
                    {
                        Vector2 candidate = p.ToVector2() * 16f;
                        Rectangle box = new((int)candidate.X, (int)candidate.Y, player.width, player.height);
                        if (p.X < 5 || p.Y < 5 || p.X + 4 >= Main.maxTilesX || p.Y + 5 >= Main.maxTilesY ||
                            PixelArea(area).Intersects(box) || Collision.SolidCollision(candidate, player.width, player.height)) continue;
                        bool dry = true;
                        for (int y = p.Y; y <= p.Y + 3; y++)
                            for (int x = p.X; x <= p.X + 2; x++)
                                if (Main.tile[x,y].LiquidAmount > 0) dry = false;
                        if (dry) { exit = candidate; return true; }
                    }
                }
            exit = default;
            return false;
        }

        private static void Build(Rectangle area)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    Tile tile = Main.tile[area.X + x, area.Y + y];
                    tile.ClearEverything();
                    int code = _template[y,x];
                    // 第一个单元是本模组恢复标记，位于最外层角落，不参与Boss内部定位。
                    // 旅人归途暂时缺失时该标记仍可识别，且不会把已清理后的新建筑误当旧场地。
                    tile.TileType = (ushort)(x == 0 && y == 0 ? _markerType :
                        code == 0 ? _insideType : code == 1 ? _borderType : _doorType);
                    tile.WallType = (ushort)_wallType;
                    tile.HasTile = true;
                    tile.IsActuated = code == 0; // 不依赖客户端PostDraw，专用服务器也必须可通行。
                }
        }

        private static void SyncTiles(Rectangle area)
        {
            if (Main.netMode != NetmodeID.Server) return;
            for (int y = area.Top; y < area.Bottom; y += 20)
                for (int x = area.Left; x < area.Right; x += 20)
                    NetMessage.SendTileSquare(-1, x, y, Math.Min(20, area.Right - x), Math.Min(20, area.Bottom - y));
        }

        private sealed class Snapshot
        {
            internal Guid Id;
            internal Rectangle Area;
            internal Vector2 Exit;
            internal byte[] Data;
            internal long Created;
            private static int CellSize => Unsafe.SizeOf<TileTypeData>() + Unsafe.SizeOf<WallTypeData>() +
                Unsafe.SizeOf<LiquidData>() + Unsafe.SizeOf<TileWallWireStateData>() + Unsafe.SizeOf<TileWallBrightnessInvisibilityData>();

            internal static Snapshot Capture(Rectangle area, Vector2 exit)
            {
                var result = new Snapshot { Id = Guid.NewGuid(), Area = area, Exit = exit,
                    Created = DateTime.UtcNow.Ticks, Data = new byte[Width * Height * CellSize] };
                int offset = 0;
                for (int y = area.Top; y < area.Bottom; y++)
                    for (int x = area.Left; x < area.Right; x++)
                    {
                        Tile tile = Main.tile[x,y];
                        CopyOut<TileTypeData>(tile, result.Data, ref offset);
                        CopyOut<WallTypeData>(tile, result.Data, ref offset);
                        CopyOut<LiquidData>(tile, result.Data, ref offset);
                        CopyOut<TileWallWireStateData>(tile, result.Data, ref offset);
                        CopyOut<TileWallBrightnessInvisibilityData>(tile, result.Data, ref offset);
                    }
                return result;
            }
            internal void Restore()
            {
                Validate();
                int offset = 0;
                for (int y = Area.Top; y < Area.Bottom; y++)
                    for (int x = Area.Left; x < Area.Right; x++)
                    {
                        if (x == Area.Left && y == Area.Top) { offset += CellSize; continue; }
                        Tile tile = Main.tile[x,y];
                        tile.ClearEverything();
                        CopyIn<TileTypeData>(tile, Data, ref offset);
                        CopyIn<WallTypeData>(tile, Data, ref offset);
                        CopyIn<LiquidData>(tile, Data, ref offset);
                        CopyIn<TileWallWireStateData>(tile, Data, ref offset);
                        CopyIn<TileWallBrightnessInvisibilityData>(tile, Data, ref offset);
                    }
                // 最后恢复标记单元；恢复中途失败仍可再次识别场地并重试完整快照。
                offset = 0;
                Tile first = Main.tile[Area.X, Area.Y];
                first.ClearEverything();
                CopyIn<TileTypeData>(first, Data, ref offset);
                CopyIn<WallTypeData>(first, Data, ref offset);
                CopyIn<LiquidData>(first, Data, ref offset);
                CopyIn<TileWallWireStateData>(first, Data, ref offset);
                CopyIn<TileWallBrightnessInvisibilityData>(first, Data, ref offset);
            }
            private static void CopyOut<T>(Tile tile, byte[] bytes, ref int offset) where T : unmanaged, ITileData
            {
                T value = tile.Get<T>();
                MemoryMarshal.Write(bytes.AsSpan(offset), in value);
                offset += Unsafe.SizeOf<T>();
            }
            private static void CopyIn<T>(Tile tile, byte[] bytes, ref int offset) where T : unmanaged, ITileData
            {
                tile.Get<T>() = MemoryMarshal.Read<T>(bytes.AsSpan(offset));
                offset += Unsafe.SizeOf<T>();
            }
            private void Validate()
            {
                if (!ValidArea(Area) || Data == null || Data.Length != Width * Height * CellSize ||
                    !float.IsFinite(Exit.X) || !float.IsFinite(Exit.Y) || Exit.X < 0 || Exit.Y < 0 ||
                    Exit.X >= Main.maxTilesX * 16f || Exit.Y >= Main.maxTilesY * 16f)
                    throw new InvalidDataException("封印快照尺寸、布局或坐标不匹配。");
            }
            internal TagCompound Save() => new()
            {
                ["id"] = Id.ToString(), ["x"] = Area.X, ["y"] = Area.Y,
                ["exitX"] = Exit.X, ["exitY"] = Exit.Y, ["created"] = Created,
                ["cellSize"] = CellSize, ["data"] = Data
            };
            internal static Snapshot Load(TagCompound tag)
            {
                if (tag.GetInt("cellSize") != CellSize) throw new InvalidDataException("快照TileData布局已改变，保留日志等待恢复。");
                var snapshot = new Snapshot { Id = Guid.Parse(tag.GetString("id")),
                    Area = new Rectangle(tag.GetInt("x"), tag.GetInt("y"), Width, Height),
                    Exit = new Vector2(tag.GetFloat("exitX"), tag.GetFloat("exitY")),
                    Created = tag.GetLong("created"), Data = tag.GetByteArray("data") };
                snapshot.Validate();
                return snapshot;
            }
        }
    }
}
