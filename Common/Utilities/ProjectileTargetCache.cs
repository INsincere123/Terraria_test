using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.Common.Utilities
{
    /// <summary>每实体索敌缓存；只缓存目标身份，位置、可追踪性和范围每次查询都重新核对。</summary>
    internal struct ProjectileTargetCache
    {
        private bool _hasSearched;
        private int _targetIndex;
        private int _targetType;
        private NPC _targetEntity;
        private int _preferredIndex;
        private ulong _lastSearchFrame;

        public int FindNearest(Projectile projectile, Vector2 origin, float range, out bool changed,
            bool prioritizeMinionTarget = false, ulong retargetFrames = 6,
            int preferredTarget = -1, float preferredRange = 0f,
            bool requireLineOfSight = false, bool includePreferredBoundary = false)
        {
            int oldIndex = _hasSearched ? _targetIndex : -1;
            int oldType = _targetType;
            NPC oldEntity = _targetEntity;
            int preferred = preferredTarget;
            if (prioritizeMinionTarget && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[projectile.owner];
                preferred = owner.active && owner.HasMinionAttackTargetNPC ? owner.MinionAttackTargetNPC : -1;
            }

            float priorityRange = preferredRange > 0f ? preferredRange : range;
            if (!IsValid(projectile, origin, preferred, priorityRange, includePreferredBoundary)) preferred = -1;

            if (preferred >= 0)
            {
                // 指定目标不等待扫描周期，并沿用 Antares 等本体的指定目标穿墙规则。
                // 取消/切换指定目标也立即重新选敌。
                _targetIndex = preferred;
                _targetType = Main.npc[preferred].type;
                _lastSearchFrame = Main.GameUpdateCount;
            }
            else
            {
                bool valid = _hasSearched && IsValid(projectile, origin, _targetIndex, range)
                    && object.ReferenceEquals(Main.npc[_targetIndex], _targetEntity)
                    && Main.npc[_targetIndex].type == _targetType
                    && (!requireLineOfSight || HasLineOfSight(origin, Main.npc[_targetIndex]));
                ulong interval = oldIndex < 0 && retargetFrames > 6 ? 6 : retargetFrames;
                // 按弹幕槽位错开周期扫描；首轮到下一相位最多等待 interval 帧。
                // 首次查询、目标失效和指定目标取消仍绕过周期，extraUpdates 不重复扫描。
                ulong searchDelay = interval;
                if (interval > 0)
                {
                    ulong phase = (ulong)(uint)projectile.whoAmI % interval;
                    ulong lastPhase = _lastSearchFrame % interval;
                    searchDelay = phase > lastPhase ? phase - lastPhase : interval - (lastPhase - phase);
                }
                bool canReuse = _hasSearched && _preferredIndex < 0 && (oldIndex < 0 || valid)
                    && Main.GameUpdateCount - _lastSearchFrame < searchDelay;
                if (!canReuse)
                {
                    _targetIndex = -1;
                    float bestDistanceSq = range * range;
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC npc = Main.npc[i];
                        if (!npc.CanBeChasedBy(projectile)) continue;
                        float distanceSq = Vector2.DistanceSquared(origin, npc.Center);
                        if (distanceSq >= bestDistanceSq) continue;
                        if (requireLineOfSight && !HasLineOfSight(origin, npc)) continue;
                        bestDistanceSq = distanceSq;
                        _targetIndex = i;
                    }
                    _targetType = _targetIndex >= 0 ? Main.npc[_targetIndex].type : 0;
                    _lastSearchFrame = Main.GameUpdateCount;
                }
            }

            _hasSearched = true;
            _preferredIndex = preferred;
            _targetEntity = _targetIndex >= 0 ? Main.npc[_targetIndex] : null;
            changed = _targetIndex != oldIndex || _targetType != oldType
                || !object.ReferenceEquals(_targetEntity, oldEntity);
            return _targetIndex;
        }

        private static bool IsValid(Projectile projectile, Vector2 origin, int index, float range,
            bool includeBoundary = false)
        {
            if (index < 0 || index >= Main.maxNPCs || !Main.npc[index].CanBeChasedBy(projectile)) return false;
            float distanceSq = Vector2.DistanceSquared(origin, Main.npc[index].Center);
            return includeBoundary ? distanceSq <= range * range : distanceSq < range * range;
        }

        // 与 Antares 原实现保持一致，使用点到点 CanHit，不替换成 CanHitLine。
        private static bool HasLineOfSight(Vector2 origin, NPC npc)
            => Collision.CanHit(origin, 0, 0, npc.Center, 0, 0);
    }
}
