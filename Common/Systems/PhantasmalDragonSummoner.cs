using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Projectiles.Minions;

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
    ///
    /// 不调用即视为该装备未启用 → 龙在2帧内自然消亡。
    ///
    /// ── 多装备策略 ──────────────────────────────────────────────
    /// 同一玩家被多件装备同时调用时，伤害与击退取最大值。
    /// 每帧 PreUpdatePlayers 会先把所有龙的 damage/knockback 重置为 0，
    /// 然后由各装备的 MaintainFor 调用取 max 叠加，确保脱卸装备后数值会正确更新。
    /// ───────────────────────────────────────────────────────────
    /// </summary>
    public static class PhantasmalDragonSummoner
    {
        public const int SegmentCount = 13;   // Head, body chain, tail.

        // ══════════════════════════════════════════════════════════════
        //   外部接口：维持指定玩家的幻影龙存在
        //
        //   · 全部5节存活 → 刷新 timeLeft 并取 max 更新数值
        //   · 任一节缺失 → 杀掉残链 + 重新生成
        // ══════════════════════════════════════════════════════════════
        public static void MaintainFor(Player player, int damage, float knockback)
        {
            if (player == null || !player.active || player.dead) return;

            int segType = ModContent.ProjectileType<DragonSegment>();

            // 统计现有节点
            int[] foundIndices = new int[SegmentCount];
            for (int i = 0; i < SegmentCount; i++) foundIndices[i] = -1;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI || p.type != segType) continue;

                int segIdx = (int)p.ai[1];
                if (segIdx >= 0 && segIdx < SegmentCount && foundIndices[segIdx] < 0)
                {
                    foundIndices[segIdx] = i;

                    // 刷新存活
                    p.timeLeft = 2;

                    // 取最大值更新数值（兼容多装备同时调用）
                    if (damage > p.damage)         p.damage    = damage;
                    if (knockback > p.knockBack)   p.knockBack = knockback;
                }
            }

            // 检查完整性
            bool allPresent = true;
            for (int i = 0; i < SegmentCount; i++)
                if (foundIndices[i] < 0) { allPresent = false; break; }

            if (allPresent) return;

            // 缺失任一节点 → 重建
            KillExisting(player, segType);
            SpawnChain(player, segType, damage, knockback);
        }

        // ══════════════════════════════════════════════════════════════
        //   内部：清除该玩家所有现存的龙节点
        // ══════════════════════════════════════════════════════════════
        private static void KillExisting(Player player, int segType)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.type == segType)
                    p.Kill();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   内部：从头到尾依次生成5节，链式传递 ai[0]
        // ══════════════════════════════════════════════════════════════
        private static void SpawnChain(Player player, int segType, int damage, float knockback)
        {
            var source = player.GetSource_FromThis();

            // 从玩家右侧水平排开生成
            Vector2 headSpawn = player.Center + new Vector2(60f, -40f);
            Vector2 initVel   = new Vector2(8f, 0f);

            int prevWhoAmI = -1;

            for (int seg = 0; seg < SegmentCount; seg++)
            {
                Vector2 spawnPos = headSpawn + new Vector2(-DragonSegment.SegmentDist * seg, 0f);
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
                    ai1: seg            // 段索引 0~4
                );

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

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != segType) continue;

                p.damage    = 0;
                p.knockBack = 0f;
            }
        }
    }
}
