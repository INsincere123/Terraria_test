using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Players;
using TestMod.Content.Items.Weapons.Plus;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // 值类型随 GlobalProjectile.Clone 复制；不持有共享的可变来源状态。
        internal TrackingWeaponKind TrackingWeapon;
        private bool _minionAttackSource;
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

        private static bool HasSpiritGuidanceForShot(Player player, Projectile p) =>
            HasSpiritGuidance(player) && (p.type < ProjectileID.Count ||
                player.GetModPlayer<SpiritGuidancePlayer>().ModdedShotsEnabled);

        // 独立记录仆从攻击来源，不复用包含哨兵的extraUpdates来源位，也不读取当前手持武器。
        private static bool IsMinionAttackCarrier(Projectile p) => !p.hostile && !p.npcProj && !p.trap &&
            !p.minion && p.minionSlots <= 0f && !p.sentry &&
            !ProjectileID.Sets.MinionSacrificable[p.type] && !ProjectileID.Sets.StardustDragon[p.type] &&
            !ProjectileID.Sets.SentryShot[p.type] && !ProjectileID.Sets.IsAWhip[p.type] &&
            p.aiStyle != ProjAIStyleID.Hook;

        private static bool IsMinionAttackParent(Projectile parent)
        {
            if (parent.hostile || parent.npcProj || parent.trap || parent.sentry ||
                ProjectileID.Sets.SentryShot[parent.type] || ProjectileID.Sets.IsAWhip[parent.type] ||
                parent.aiStyle == ProjAIStyleID.Hook) return false;
            bool hasState = TryGetTrackingParentState(parent, out GlobalProjectile state);
            if (hasState && (state.IsTimeEchoAttack || state.IsExtraHit)) return false;
            return parent.minion || parent.minionSlots > 0f || ProjectileID.Sets.MinionSacrificable[parent.type] ||
                ProjectileID.Sets.StardustDragon[parent.type] || ProjectileID.Sets.MinionShot[parent.type] ||
                hasState && state._minionAttackSource;
        }

        private bool IsSpiritGuidanceSourceShot(Projectile p) => _minionAttackSource &&
            !IsTimeEchoAttack && !IsExtraHit && p.friendly && p.damage > 0 && IsMinionAttackCarrier(p);

        private static bool TryGetTrackingParentState(Projectile parent, out GlobalProjectile state)
        {
            state = null;
            int index = ModContent.GetInstance<GlobalProjectile>().PerEntityIndex;
            System.ReadOnlySpan<Terraria.ModLoader.GlobalProjectile> globals = parent.EntityGlobals;
            return index >= 0 && index < globals.Length && parent.TryGetGlobalProjectile(out state);
        }

        private void TrackingSources_OnSpawn(Projectile p, IEntitySource source)
        {
            if (IsTimeEchoAttack || IsExtraHit || p.hostile || p.npcProj || p.trap ||
                p.owner < 0 || p.owner >= Main.maxPlayers) return;
            _minionAttackSource = IsMinionAttackCarrier(p) &&
                source is EntitySource_Parent { Entity: Projectile attackParent } && attackParent.owner == p.owner &&
                IsMinionAttackParent(attackParent);
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
                if (TryGetTrackingParentState(parent, out GlobalProjectile state) &&
                    !state.IsTimeEchoAttack && !state.IsExtraHit && TrackingWeaponMatches(state.TrackingWeapon, p.type))
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
            if ((TrackingWeapon != TrackingWeaponKind.None || _minionAttackSource) && p.owner == Main.myPlayer) p.netUpdate = true;
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
