using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using TestMod.Common.Mechanics.AccessoryEffects;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        private const float HomingRange     = 900f;   // 索敌范围（像素，约 56 格）
        private const float HomingTurnSpeed = 0.10f;  // 每次 AI 更新的转向系数（0=不转，1=瞬间）
        // ─────────────────────────────────────────────────────────────

        /// <summary>此弹幕是否被标记为强制追踪（由 OnSpawn 写入）。</summary>
        public bool IsHomingTagged;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            ProjectileLookup.Observe(projectile, this);
            if (TimeEcho_OnSpawn(projectile, source)) return;
            // 只对玩家直接使用物品生成的弹幕打标记；衍生弹幕不消耗追踪次数。
            if (source is not EntitySource_ItemUse) return;
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;
            if (projectile.owner != Main.myPlayer) return;
            if (IsForcedHomingExcluded(projectile)) return;

            Player player = Main.player[projectile.owner];
            if (!player.active) return;

            var mp = player.GetModPlayer<OmniEffectsPlayer>();

            if (mp.ForcedHomingRemaining > 0)
            {
                IsHomingTagged = true;
                mp.ForcedHomingRemaining--;
                projectile.netUpdate = true;
            }
        }

        private static bool IsForcedHomingExcluded(Projectile projectile)
        {
            // 法杖直接生成的仆从同样使用 ItemUse 来源，需按本体标志排除，保留其原有 AI。
            return projectile.minion
                || projectile.sentry
                || projectile.minionSlots > 0f
                || ProjectileID.Sets.MinionSacrificable[projectile.type]
                || projectile.aiStyle == ProjAIStyleID.Hook
                || ProjectileID.Sets.IsAWhip[projectile.type]
                || (projectile.type == ProjectileID.Daybreak && projectile.ai[0] != 0f);
        }

        /// <summary>
        /// 强制追踪逻辑，由 GlobalProjectile.PostAI 末尾调用。
        /// 每帧将弹幕速度平滑转向最近目标，保持原速大小。
        /// </summary>
        internal void ForcedHoming_Update(Projectile projectile)
        {
            if (!IsHomingTagged) return;
            if (IsForcedHomingExcluded(projectile)) return;
            if (projectile.velocity == Vector2.Zero) return;

            int targetIdx = _forcedHomingTargetCache.FindNearest(projectile, projectile.Center, HomingRange,
                out bool targetChanged);
            _trackingTargetChanged |= targetChanged;
            _trackingHasTarget |= targetIdx >= 0;
            if (targetIdx < 0) return;

            NPC target = Main.npc[targetIdx];
            Vector2 toTarget = target.Center - projectile.Center;
            if (toTarget.LengthSquared() <= 1f) return;
            float speed = projectile.velocity.Length();
            Vector2 direction = toTarget.SafeNormalize(Vector2.UnitX);
            Vector2 desired = direction * speed;

            projectile.velocity = Vector2.Lerp(projectile.velocity, desired, HomingTurnSpeed)
                .SafeNormalize(direction) * speed;
        }
    }
}
