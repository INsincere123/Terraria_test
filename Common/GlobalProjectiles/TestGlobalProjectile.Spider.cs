using Terraria;
using Terraria.ID;
using TestMod.Common.Mechanics.Minions;

namespace TestMod.Common.GlobalProjectiles
{
    /// <summary>
    /// 蜘蛛法杖召唤物强化 — 薄包装层。
    ///
    /// 涉及弹射物：
    ///   390 VenomSpider / 391 JumperSpider / 392 DangerousSpider / AbigailMinion
    ///
    /// 所有逻辑已迁移至 MinionSystem（Common/Mechanics/Minions/）。
    /// 本文件仅作 GlobalProjectile 公共 API 的桥接。
    /// </summary>
    public partial class TestGlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   分类判定（转发）
        // ══════════════════════════════════════════════════════════════

        public static bool IsSpiderMinion(int type) => MinionSystem.IsSpiderMinion(type);

        // ══════════════════════════════════════════════════════════════
        //   行为（转发）
        // ══════════════════════════════════════════════════════════════

        public static void ApplySpiderSetDefaults(Projectile projectile)
            => MinionSystem.ApplySpiderSetDefaults(projectile);

        public static void ApplySpiderPostAI(Projectile projectile, Player player)
            => MinionSystem.ApplySpiderPostAI(projectile, player);
    }
}
