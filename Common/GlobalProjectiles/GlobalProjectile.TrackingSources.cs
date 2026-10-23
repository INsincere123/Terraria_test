using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using TestMod.Common.DataStructures;
using TestMod.Common.Players;
using TestMod.Content.Items.Weapons.Plus;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // 值类型随 GlobalProjectile.Clone 复制；不持有共享的可变来源状态。
        internal TrackingWeaponKind TrackingWeapon;
        private bool _trackingDefaultsApplied;
        private bool _contactImmunityApplied, _savedLocalImmunity, _savedStaticImmunity;
        private int _savedHitCooldown;

        private static bool TrackingWeaponMatches(TrackingWeaponKind kind, int type) => kind switch
        {
            TrackingWeaponKind.DesertTiger => type == ProjectileID.StormTigerGem || ProjectileID.Sets.StormTiger[type],
            TrackingWeaponKind.Raven => type == ProjectileID.Raven,
            TrackingWeaponKind.Abigail => type is ProjectileID.AbigailCounter or ProjectileID.AbigailMinion,
            TrackingWeaponKind.Spider => IsSpiderMinion(type),
            _ => false
        };

        internal bool HasTrackingWeapon(TrackingWeaponKind kind)
            => TrackingWeapon == kind && kind != TrackingWeaponKind.None && !IsTimeEchoAttack && !IsExtraHit;

        private bool HasEnhancedTerraprisma => HasPlusWeapon(PlusWeaponKind.Terraprisma);
        private bool HasEnhancedDragon => HasPlusWeapon(PlusWeaponKind.StardustDragon);

        private static bool HasSpiritGuidance(Player player)
            => player.active && !player.dead && player.TryGetModPlayer(out SpiritGuidancePlayer state) && state.Enabled;

        private void TrackingSources_OnSpawn(Projectile p, IEntitySource source)
        {
            if (IsTimeEchoAttack || IsExtraHit || p.hostile || p.npcProj || p.trap ||
                p.owner < 0 || p.owner >= Main.maxPlayers) return;
            if (source is EntitySource_ItemUse use && use.Player.whoAmI == p.owner)
            {
                if (use.Item.ModItem is TrackingSummonWeapon weapon)
                    TrackingWeapon = TrackingWeaponMatches(weapon.TrackingKind, p.type) ? weapon.TrackingKind : TrackingWeaponKind.None;
                // OnSpawn 只在实际发射后执行；查询、缺魔力及右键指定目标不会清理召唤物。
                if (p.owner == Main.myPlayer && p.type is ProjectileID.StormTigerGem or ProjectileID.AbigailCounter)
                    PrepareCountedSummon(p);
            }
            else if (source is EntitySource_Parent { Entity: Projectile parent } && parent.owner == p.owner)
            {
                var state = parent.GetGlobalProjectile<GlobalProjectile>();
                if (!state.IsTimeEchoAttack && !state.IsExtraHit && TrackingWeaponMatches(state.TrackingWeapon, p.type))
                    TrackingWeapon = state.TrackingWeapon;
            }
            else if (source is EntitySource_Misc misc && p.owner == Main.myPlayer)
            {
                // 原版玩家更新根据宝石/计数弹幕创建本体，形态升级会再次走此来源。
                int counter = misc.Context == "StormTigerTierSwap" && ProjectileID.Sets.StormTiger[p.type]
                    ? ProjectileID.StormTigerGem
                    : misc.Context == "AbigailTierSwap" && p.type == ProjectileID.AbigailMinion
                        ? ProjectileID.AbigailCounter : -1;
                TrackingWeaponKind candidate = counter == ProjectileID.StormTigerGem
                    ? TrackingWeaponKind.DesertTiger : TrackingWeaponKind.Abigail;
                bool found = false;
                if (counter >= 0)
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile entity = Main.projectile[i];
                        if (!entity.active || entity.owner != p.owner || entity.type != counter) continue;
                        if (!entity.GetGlobalProjectile<GlobalProjectile>().HasTrackingWeapon(candidate)) return;
                        found = true;
                    }
                if (found) TrackingWeapon = candidate;
            }
            ApplyTrackingSourceDefaults(p);
            if (TrackingWeapon != TrackingWeaponKind.None && p.owner == Main.myPlayer) p.netUpdate = true;
        }

        private void PrepareCountedSummon(Projectile spawned)
        {
            TrackingWeaponKind kind = spawned.type == ProjectileID.StormTigerGem
                ? TrackingWeaponKind.DesertTiger : TrackingWeaponKind.Abigail;
            bool empowered = HasTrackingWeapon(kind);
            bool replace = false;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (other.active && other.owner == spawned.owner && other.whoAmI != spawned.whoAmI &&
                    TrackingWeaponMatches(kind, other.type) &&
                    other.GetGlobalProjectile<GlobalProjectile>().HasTrackingWeapon(kind) != empowered)
                    replace = true;
            }
            if (!replace) return;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (other.active && other.owner == spawned.owner && other.whoAmI != spawned.whoAmI &&
                    TrackingWeaponMatches(kind, other.type)) other.Kill();
            }
            // ownedProjectileCounts 是上一轮玩家扫描值，清理后必须避免旧计数延迟一帧生成错误形态。
            Player player = Main.player[spawned.owner];
            for (int type = 0; type < ProjectileID.Count; type++)
                if (TrackingWeaponMatches(kind, type)) player.ownedProjectileCounts[type] = 0;
            player.ownedProjectileCounts[spawned.type] = 1;
            if (kind == TrackingWeaponKind.DesertTiger) player.highestStormTigerGemOriginalDamage = spawned.originalDamage;
            else player.highestAbigailCounterOriginalDamage = spawned.originalDamage;
        }

        private void ApplyTrackingSourceDefaults(Projectile p)
        {
            if (_trackingDefaultsApplied) return;
            if (p.type == ProjectileID.EmpressBlade && HasEnhancedTerraprisma)
                ApplyTerraprismaDefaults(p);
            else if (IsSpiderMinion(p.type) && HasTrackingWeapon(TrackingWeaponKind.Spider))
                ApplySpiderSetDefaults(p);
            else return;
            _trackingDefaultsApplied = true;
        }

        private void UpdateContactTrackingImmunity(Projectile p)
        {
            bool enabled = !IsTimeEchoAttack && !IsExtraHit && IsContactMinion(p) &&
                p.owner >= 0 && p.owner < Main.maxPlayers && HasSpiritGuidance(Main.player[p.owner]);
            if (enabled && !_contactImmunityApplied)
            {
                _savedLocalImmunity = p.usesLocalNPCImmunity;
                _savedStaticImmunity = p.usesIDStaticNPCImmunity;
                _savedHitCooldown = p.localNPCHitCooldown;
                ApplyContactMinionSetDefaults(p);
                _contactImmunityApplied = true;
            }
            else if (!enabled && _contactImmunityApplied)
            {
                // 仅恢复仍由本次接管持有的值，尊重其他来源随后写入的设置；不清除免疫数组。
                if (p.usesLocalNPCImmunity) p.usesLocalNPCImmunity = _savedLocalImmunity;
                if (!p.usesIDStaticNPCImmunity) p.usesIDStaticNPCImmunity = _savedStaticImmunity;
                if (p.localNPCHitCooldown == CONTACT_MINION_HIT_COOLDOWN) p.localNPCHitCooldown = _savedHitCooldown;
                _contactImmunityApplied = false;
                _trackingTargetCache = default;
            }
        }
    }
}
