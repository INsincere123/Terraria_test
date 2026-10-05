using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Projectiles.Minions;
using System;
using System.Collections.Generic;

namespace TestMod.Common.Systems
{
    /// <summary>
    /// 幻影龙独立召唤系统
    ///
    /// ── 使用方式 ────────────────────────────────────────────────
    /// 任何装备只需在自己每帧执行的钩子里（UpdateArmorSet / UpdateAccessory /
    /// UpdateEquip / HoldItem 等）加一行：
    ///
    ///     PhantasmalDragonSummoner.MaintainFor(player, damage, knockback);
    ///     PhantasmalDragonSummoner.MaintainTwinFor(player, damagePerChain, knockback);
    ///
    /// 不调用即视为该装备未启用 → 龙在2帧内自然消亡。
    ///
    /// ── 多装备策略 ──────────────────────────────────────────────
    /// 同一玩家的同一条链被多件装备同时调用时，伤害与击退取最大值。
    /// 每帧 PreUpdatePlayers 会先把所有龙的 damage/knockback 重置为 0，
    /// 然后由各装备的 MaintainFor 调用取 max 叠加，确保脱卸装备后数值会正确更新。
    /// ───────────────────────────────────────────────────────────
    /// </summary>
    public static class PhantasmalDragonSummoner
    {
        public const int SegmentCount = 13;   // Head, body chain, tail.
        public const int TwinSegmentCount = 7; // 六个间距，正好是原链长的一半。
        public const int SingleChain = 0;
        public const int TwinNormalChain = 1;
        public const int TwinInvertedChain = 2;

        private static readonly Dictionary<(int Owner, int Chain), Projectile[]> _segmentsByOwner = new();

        internal static int GetSegmentCount(int chain) => chain == SingleChain ? SegmentCount : TwinSegmentCount;
        internal static int GetHorizontalDirection(int chain) => chain == TwinInvertedChain ? -1 : 1;

        // 所有调用者复用段数组；每次仅从所属列表填充，不扫描全场弹幕。
        internal static Projectile[] GetSegments(int owner, int chain = SingleChain, bool preferLast = false)
        {
            var key = (owner, chain);
            int segmentCount = GetSegmentCount(chain);
            if (!_segmentsByOwner.TryGetValue(key, out Projectile[] segments))
            {
                segments = new Projectile[segmentCount];
                _segmentsByOwner.Add(key, segments);
            }
            Array.Clear(segments, 0, segments.Length);
            int segType = ModContent.ProjectileType<DragonSegment>();
            foreach (Projectile projectile in ProjectileLookup.Owned(owner, segType))
            {
                if (projectile.ai[2] != chain) continue;
                int index = (int)projectile.ai[1];
                if (index >= 0 && index < segmentCount && (preferLast || segments[index] == null))
                    segments[index] = projectile;
            }
            return segments;
        }

        internal static void ClearSegments() => _segmentsByOwner.Clear();

        // ══════════════════════════════════════════════════════════════
        //   外部接口：维持指定玩家的幻影龙存在
        //
        //   · 全部13节存活 → 刷新 timeLeft 并取 max 更新数值
        //   · 任一节缺失 → 杀掉残链 + 重新生成
        // ══════════════════════════════════════════════════════════════
        public static void MaintainFor(Player player, int damage, float knockback)
            => MaintainChain(player, damage, knockback, SingleChain);

        // damage 是每条链每个可伤害节点的伤害，由装备侧决定是否减半。
        public static void MaintainTwinFor(Player player, int damage, float knockback)
        {
            MaintainChain(player, damage, knockback, TwinNormalChain);
            MaintainChain(player, damage, knockback, TwinInvertedChain);
        }

        private static void MaintainChain(Player player, int damage, float knockback, int chain)
        {
            if (player == null || !player.active || player.dead) return;

            int segType = ModContent.ProjectileType<DragonSegment>();

            // 统计现有节点
            Projectile[] segments = GetSegments(player.whoAmI, chain);
            bool allPresent = true;
            foreach (Projectile p in segments)
            {
                if (p == null)
                {
                    allPresent = false;
                    continue;
                }
                p.timeLeft = 2;
                if (damage > p.damage) p.damage = damage;
                if (knockback > p.knockBack) p.knockBack = knockback;
            }

            // 远端可以刷新收到的节点，但只有 owner 能销毁残链和生成替代链。
            if (allPresent || player.whoAmI != Main.myPlayer) return;

            // 缺失任一节点 → 重建
            KillExisting(player, segType, chain);
            SpawnChain(player, segType, damage, knockback, chain);
        }

        // ══════════════════════════════════════════════════════════════
        //   内部：只清除指定玩家、指定链的残余节点
        // ══════════════════════════════════════════════════════════════
        private static void KillExisting(Player player, int segType, int chain)
        {
            foreach (Projectile p in ProjectileLookup.Owned(player.whoAmI, segType))
            {
                if (p.ai[2] == chain) p.Kill();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   内部：按链类型生成13或7节，链式传递 ai[0]
        // ══════════════════════════════════════════════════════════════
        private static void SpawnChain(Player player, int segType, int damage, float knockback, int chain)
        {
            var source = player.GetSource_FromThis();

            // 原色链在右侧、反色链在左侧，各自向外起飞。
            int direction = GetHorizontalDirection(chain);
            Vector2 headSpawn = player.Center + new Vector2(60f * direction, -40f);
            Vector2 initVel   = new Vector2(8f * direction, 0f);

            int prevWhoAmI = -1;

            for (int seg = 0; seg < GetSegmentCount(chain); seg++)
            {
                Vector2 spawnPos = headSpawn + new Vector2(-DragonSegment.SegmentDist * seg * direction, 0f);
                Vector2 vel      = (seg == 0) ? initVel : Vector2.Zero;

                int whoAmI = Projectile.NewProjectile(
                    source,
                    spawnPos,
                    vel,
                    segType,
                    damage,
                    knockback,
                    player.whoAmI,
                    ai0: prevWhoAmI,    // 前节点索引（头节点为 -1）
                    ai1: seg,           // 链内段索引：单龙0~12，双龙0~6
                    ai2: chain          // 0=单龙，1=双龙原色链，2=双龙反色链
                );

                // 弹幕槽不足时不把无效索引串入下一段；下次维持会重建残链。
                if (whoAmI < 0 || whoAmI >= Main.maxProjectiles) break;
                prevWhoAmI = whoAmI;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //   每帧重置数值的 ModSystem
    //
    //   时机：PreUpdatePlayers — 在所有玩家逻辑之前
    //   作用：把全场所有幻影龙节点的 damage/knockBack 归零，
    //         留待本帧各装备 MaintainFor 调用取 max 累加。
    //
    //   这样脱掉强装备换弱装备时，伤害会正确下调而不是卡在最大值。
    // ══════════════════════════════════════════════════════════════
    public class PhantasmalDragonResetSystem : ModSystem
    {
        public override void PreUpdatePlayers()
        {
            int segType = ModContent.ProjectileType<DragonSegment>();

            foreach (Projectile p in ProjectileLookup.OfType(segType))
            {
                p.damage    = 0;
                p.knockBack = 0f;
            }
        }

        public override void OnWorldLoad() => PhantasmalDragonSummoner.ClearSegments();
        public override void OnWorldUnload() => PhantasmalDragonSummoner.ClearSegments();
        public override void Unload() => PhantasmalDragonSummoner.ClearSegments();
    }
}
