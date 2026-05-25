using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Content.Tiles;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 黄泉馈灵塔核心调度器（ModSystem）。
    /// 职责：祭坛扫描、Boss 激活判定、黄泉 Buff 刷新、
    ///       队友死亡检测（→ 汲命 buff）、周期性脉冲伤害。
    /// </summary>
    public class ArenaAltarSystem : ModSystem
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const float AltarRangeTiles     = 160f;       // 祭坛作用范围（格）
        public const float AltarRangePx        = AltarRangeTiles * 16f; // 转像素

        public const int   PulseIntervalFrames = 5 * 60;    // 脉冲间隔（帧）：5 秒
        public const int   PulseDamage         = 100;       // 脉冲对普通敌方的平伤
        public const int   PulseBossExtraDmg   = 500;       // 脉冲对 Boss 的额外平伤（叠加）

        public const float KillBoostDuration   = 2.0f;      // 杀意 buff 持续（秒）
        public const float LifestealDuration   = 1.5f;      // 汲命 buff 持续（秒）
        // ─────────────────────────────────────────────────────────────

        // 已发现的祭坛左上角格坐标（每秒重扫刷新）
        private static readonly HashSet<Point> _altarPositions       = new();
        // 当前激活的祭坛（范围内存在存活 Boss）
        private static readonly HashSet<Point> _activeAltarPositions = new();

        private int          _altarScanTimer = 0;                       // 祭坛扫描计时器（帧）
        private int          _pulseTimer     = 0;                       // 脉冲伤害计时器（帧）
        private readonly bool[] _prevPlayerAlive = new bool[Main.maxPlayers]; // 上帧玩家存活状态

        // ═══════════════════════════════════════════════════════════════
        //   公共 API（供 GlobalNPC、ArenaAltarPlayer 查询）
        // ═══════════════════════════════════════════════════════════════

        /// <summary>判断给定世界坐标是否处于某个「激活」祭坛（范围内有 Boss）的范围内。</summary>
        public static bool IsInActiveRange(Vector2 worldPos)
        {
            float rSq = AltarRangePx * AltarRangePx;
            foreach (var pt in _activeAltarPositions)
            {
                if (Vector2.DistanceSquared(worldPos, AltarCenter(pt)) <= rSq)
                    return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        //   PostUpdateEverything — 每帧主调度
        // ═══════════════════════════════════════════════════════════════
        public override void PostUpdateEverything()
        {
            // 每 60 帧（1 秒）重新扫描祭坛位置（成本低，避免漏检移除/新增的祭坛）
            if (++_altarScanTimer >= 60)
            {
                _altarScanTimer = 0;
                ReScanAltarPositions();
            }

            UpdateActiveAltars();   // 更新哪些祭坛当前被 Boss 激活
            RefreshHuangQuanBuff(); // 给范围内玩家刷新 黄泉 buff（不要求 Boss）
            CheckPlayerDeaths();    // 检测队友死亡 → 汲命 buff
            TickPulse();            // 脉冲伤害倒计时与执行
        }

        // ═══════════════════════════════════════════════════════════════
        //   祭坛扫描 — 在所有在线玩家周围 170 格范围内搜索 Tile
        // ═══════════════════════════════════════════════════════════════
        private void ReScanAltarPositions()
        {
            _altarPositions.Clear();
            int altarType  = ModContent.TileType<HuangQuanAltarTile>();
            int scanRadius = (int)AltarRangeTiles + 10; // 略大于作用范围，确保不遗漏

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                if (!player.active) continue;

                int cx = (int)(player.Center.X / 16);
                int cy = (int)(player.Center.Y / 16);

                int minX = Math.Max(0, cx - scanRadius);
                int maxX = Math.Min(Main.maxTilesX - 1, cx + scanRadius);
                int minY = Math.Max(0, cy - scanRadius);
                int maxY = Math.Min(Main.maxTilesY - 1, cy + scanRadius);

                for (int tx = minX; tx <= maxX; tx++)
                for (int ty = minY; ty <= maxY; ty++)
                {
                    Tile tile = Main.tile[tx, ty];
                    // 只登记左上角格（FrameX=0, FrameY=0），避免同一祭坛的其余格重复注册
                    if (tile.HasTile && tile.TileType == altarType
                        && tile.TileFrameX == 0 && tile.TileFrameY == 0)
                    {
                        _altarPositions.Add(new Point(tx, ty));
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //   激活判定 — 祭坛范围内有存活的 Boss NPC 才算激活
        // ═══════════════════════════════════════════════════════════════
        private void UpdateActiveAltars()
        {
            _activeAltarPositions.Clear();
            float rSq = AltarRangePx * AltarRangePx;

            foreach (var pt in _altarPositions)
            {
                Vector2 center = AltarCenter(pt);
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || !npc.boss || npc.friendly) continue;
                    if (Vector2.DistanceSquared(npc.Center, center) <= rSq)
                    {
                        _activeAltarPositions.Add(pt);
                        break; // 该祭坛已确认激活，跳出内层循环
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //   黄泉 Buff 刷新 — 只要在任意祭坛范围内即可获得（无需 Boss）
        // ═══════════════════════════════════════════════════════════════
        private void RefreshHuangQuanBuff()
        {
            int   buffType = ModContent.BuffType<HuangQuanBuff>();
            float rSq      = AltarRangePx * AltarRangePx;

            foreach (var pt in _altarPositions)
            {
                Vector2 center = AltarCenter(pt);
                for (int p = 0; p < Main.maxPlayers; p++)
                {
                    Player player = Main.player[p];
                    if (!player.active || player.dead) continue;
                    if (Vector2.DistanceSquared(player.Center, center) <= rSq)
                        player.AddBuff(buffType, 2); // 每帧刷新 2 帧时长，保持持续
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //   队友死亡检测 — 玩家从存活→死亡且在激活范围内时，发放汲命 buff
        // ═══════════════════════════════════════════════════════════════
        private void CheckPlayerDeaths()
        {
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player  = Main.player[p];
                bool   isAlive = player.active && !player.dead;

                // 本帧死亡 + 处于激活祭坛范围内 → 触发汲命
                if (_prevPlayerAlive[p] && !isAlive && IsInActiveRange(player.Center))
                    GrantLifestealToNearby();

                _prevPlayerAlive[p] = isAlive;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //   脉冲伤害 — 每 5 秒对激活范围内所有敌方 NPC 造成伤害
        // ═══════════════════════════════════════════════════════════════
        private void TickPulse()
        {
            if (_activeAltarPositions.Count == 0)
            {
                _pulseTimer = 0; // 无激活祭坛时重置，防止 Boss 刚进入时立刻触发
                return;
            }

            if (++_pulseTimer < PulseIntervalFrames) return;
            _pulseTimer = 0;

            // 联机下仅服务端/单机执行（SimpleStrikeNPC 内部已处理 net 包同步）
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float rSq = AltarRangePx * AltarRangePx;
            foreach (var pt in _activeAltarPositions)
            {
                Vector2 center = AltarCenter(pt);
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.townNPC) continue;
                    if (Vector2.DistanceSquared(npc.Center, center) > rSq) continue;

                    // Boss 获得额外平伤；noPlayerInteraction 避免触发玩家 OnHit 回调
                    int dmg = PulseDamage + (npc.boss ? PulseBossExtraDmg : 0);
                    npc.SimpleStrikeNPC(dmg, 0, crit: false, knockBack: 0f,
                        damageType: DamageClass.Generic,
                        damageVariation: false, noPlayerInteraction: true);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //   公共辅助方法（由 GlobalNPC.OnKill 调用）
        // ═══════════════════════════════════════════════════════════════

        /// <summary>给所有激活范围内的存活玩家施加「汲命」buff（友方 NPC 或队友死亡时调用）。</summary>
        public static void GrantLifestealToNearby()
        {
            int   buffType = ModContent.BuffType<HuangQuanLifestealBuff>();
            int   duration = (int)(LifestealDuration * 60);
            float rSq      = AltarRangePx * AltarRangePx;

            foreach (var pt in _activeAltarPositions)
            {
                Vector2 center = AltarCenter(pt);
                for (int p = 0; p < Main.maxPlayers; p++)
                {
                    Player player = Main.player[p];
                    if (!player.active || player.dead) continue;
                    if (Vector2.DistanceSquared(player.Center, center) <= rSq)
                        player.AddBuff(buffType, duration);
                }
            }
        }

        /// <summary>给所有激活范围内的存活玩家施加「杀意」buff（击杀敌方 NPC 时调用）。</summary>
        public static void GrantDamageBoostToNearby()
        {
            int   buffType = ModContent.BuffType<HuangQuanDamageBoostBuff>();
            int   duration = (int)(KillBoostDuration * 60);
            float rSq      = AltarRangePx * AltarRangePx;

            foreach (var pt in _activeAltarPositions)
            {
                Vector2 center = AltarCenter(pt);
                for (int p = 0; p < Main.maxPlayers; p++)
                {
                    Player player = Main.player[p];
                    if (!player.active || player.dead) continue;
                    if (Vector2.DistanceSquared(player.Center, center) <= rSq)
                        player.AddBuff(buffType, duration);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //   工具方法
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// 根据左上角格坐标计算祭坛的世界中心点（2 格宽 × 3 格高）。
        /// 中心 X = (tileX + 1) × 16，中心 Y = tileY × 16 + 24。
        /// </summary>
        private static Vector2 AltarCenter(Point tilePos)
            => new Vector2((tilePos.X + 1) * 16f, tilePos.Y * 16f + 24f);

        // ═══════════════════════════════════════════════════════════════
        //   世界卸载时清理静态状态（防止跨存档污染）
        // ═══════════════════════════════════════════════════════════════
        public override void OnWorldUnload()
        {
            _altarPositions.Clear();
            _activeAltarPositions.Clear();
            _pulseTimer     = 0;
            _altarScanTimer = 0;
        }
    }
}
