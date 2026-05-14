using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TestMod.Common.Mechanics.Minions;

namespace TestMod.Common.GlobalProjectiles
{
    /// <summary>
    /// 召唤物分类系统 — 薄包装层。
    ///
    /// 所有分类数据与行为逻辑已迁移至 MinionSystem（Common/Mechanics/Minions/）。
    /// 本文件仅作 GlobalProjectile 公共 API 的桥接，保持调用方代码不变。
    ///
    /// 三类说明见 MinionSystem 文档注释。
    /// </summary>
    public partial class TestGlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   数值常量（转发到 MinionSystem）
        // ══════════════════════════════════════════════════════════════
        private const float CONTACT_MINION_TARGET_RANGE = MinionSystem.ContactTargetRange;
        private const float CONTACT_MINION_RETURN_DIST  = MinionSystem.ContactReturnDist;
        private const int   CONTACT_MINION_HIT_COOLDOWN = MinionSystem.ContactHitCooldown;
        private const float CONTACT_MINION_CHASE_SPEED  = MinionSystem.ContactChaseSpeed;
        private const float CONTACT_MINION_BOUNCE_SPEED = MinionSystem.ContactBounceSpeed;

        // ══════════════════════════════════════════════════════════════
        //   数据集（转发到 MinionSystem，外部引用不受影响）
        // ══════════════════════════════════════════════════════════════

        /// <summary>② 射击型本体列表。加入后该召唤物完全保持 vanilla AI。</summary>
        public static HashSet<int> ShootingMinionBodies    => MinionSystem.ShootingMinionBodies;

        /// <summary>完全保持 vanilla 的召唤物（不追踪、不弹开、不改无敌帧）。</summary>
        public static HashSet<int> VanillaMinionExclusions => MinionSystem.VanillaMinionExclusions;

        // ══════════════════════════════════════════════════════════════
        //   分类判定（转发）
        // ══════════════════════════════════════════════════════════════

        public static bool IsSpeciallyHandledMinion(int type)     => MinionSystem.IsSpeciallyHandledMinion(type);
        public static bool IsShootingMinionBody(int type)         => MinionSystem.IsShootingMinionBody(type);
        public static bool IsContactMinion(Projectile projectile) => MinionSystem.IsContactMinion(projectile);

        // ══════════════════════════════════════════════════════════════
        //   行为（转发）
        // ══════════════════════════════════════════════════════════════

        public static void ApplyContactMinionSetDefaults(Projectile projectile)
            => MinionSystem.ApplyContactMinionSetDefaults(projectile);

        public void ApplyContactMinionTracking(Projectile projectile, Player player)
            => MinionSystem.ApplyContactMinionTracking(projectile, player);

        public static void ApplyContactMinionBounce(Projectile projectile, NPC target)
            => MinionSystem.ApplyContactMinionBounce(projectile, target);
    }
}
