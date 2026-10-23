using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using System.IO;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Common.DataStructures;
using TestMod.Content.Items.Weapons.Plus;

namespace TestMod.Common.GlobalProjectiles
{
    public class TerraArmorProjectile : Terraria.ModLoader.GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        private bool _extraUpdateApplied;
        private bool _summonDescendant;
        private bool _plusExtraUpdatesExcluded;
        private const int MaxVisiblePhantasms = 50;
        private static readonly TerraArmorProjectile[] VisiblePhantasms = new TerraArmorProjectile[Main.maxProjectiles];
        private static bool _hasVisibilityCache;
        private static ulong _visibilityTick;
        private static Rectangle _visibilityViewport;

        public override void Unload()
        {
            System.Array.Clear(VisiblePhantasms);
            _hasVisibilityCache = false;
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (projectile.type != ProjectileID.PhantasmArrow) return true;
            Rectangle viewport = new((int)Main.screenPosition.X, (int)Main.screenPosition.Y,
                Main.screenWidth, Main.screenHeight);
            viewport.Inflate(32, 32); // 箭身仍可出现在屏幕边缘时也计入额度。
            if (!_hasVisibilityCache || _visibilityTick != Main.GameUpdateCount || _visibilityViewport != viewport)
            {
                _hasVisibilityCache = true;
                _visibilityTick = Main.GameUpdateCount;
                _visibilityViewport = viewport;
                System.Array.Clear(VisiblePhantasms);
                int count = 0;
                // 每 tick / 镜头变化仅扫描一次；原版、强化武器和泰拉套等所有来源共享本地绘制额度。
                for (int i = 0; i < Main.maxProjectiles && count < MaxVisiblePhantasms; i++)
                {
                    Projectile candidate = Main.projectile[i];
                    if (!candidate.active || candidate.type != ProjectileID.PhantasmArrow ||
                        !viewport.Intersects(candidate.Hitbox)) continue;
                    TerraArmorProjectile state = candidate.GetGlobalProjectile<TerraArmorProjectile>();
                    VisiblePhantasms[i] = state;
                    count++;
                }
            }
            return ReferenceEquals(VisiblePhantasms[projectile.whoAmI], this);
        }

        internal static bool TryGetArmor(Projectile projectile, out TerraArmorPlayer armor)
        {
            armor = null;
            return projectile.owner >= 0 && projectile.owner < Main.maxPlayers &&
                Main.player[projectile.owner].active && Main.player[projectile.owner].TryGetModPlayer(out armor);
        }

        internal static bool IsSummonAttack(Projectile projectile) => !projectile.hostile &&
            !ProjectileID.Sets.IsAWhip[projectile.type] && SummonCritPlayer.IsSummonDamage(projectile);

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            _plusExtraUpdatesExcluded = source switch
            {
                EntitySource_ItemUse use => use.Player.whoAmI == projectile.owner &&
                    use.Item.ModItem is PlusWeapon or TrackingSummonWeapon,
                EntitySource_Parent { Entity: Projectile plusParent } => plusParent.owner == projectile.owner &&
                    plusParent.TryGetGlobalProjectile(out TerraArmorProjectile plusState) && plusState.IsPlusExtraUpdatesExcluded(plusParent),
                _ => false
            };
            // 射弹不一定声明 MinionShot/SentryShot；按真实父实体来源保留派生链，不依赖手持物品。
            if (source is EntitySource_Parent { Entity: Projectile parent } && parent.owner == projectile.owner &&
                !parent.hostile && !parent.npcProj && !parent.trap)
                _summonDescendant = IsSummonBodyOrShot(parent) ||
                    parent.TryGetGlobalProjectile(out TerraArmorProjectile parentState) && parentState._summonDescendant;
            TryGetArmor(projectile, out TerraArmorPlayer armor);
            if (armor != null && projectile.type == ProjectileID.CrystalLeafShot && TerraArmorPlayer.Equipped(armor.Player))
            {
                projectile.DamageType = DamageClass.Generic;
                projectile.originalDamage = Player.crystalLeafDamage;
                projectile.damage = (int)armor.Player.GetTotalDamage(DamageClass.Generic).ApplyTo(Player.crystalLeafDamage);
                projectile.CritChance = (int)armor.Player.GetTotalCritChance(DamageClass.Generic);
                projectile.ArmorPenetration = (int)armor.Player.GetTotalArmorPenetration(DamageClass.Generic);
                projectile.netUpdate = true;
            }
            UpdateExtraUpdates(projectile, armor);
            if (_summonDescendant && projectile.owner == Main.myPlayer) projectile.netUpdate = true;
        }

        private static bool IsSummonBodyOrShot(Projectile p)
            => p.minion || p.minionSlots > 0f || p.sentry || ProjectileID.Sets.MinionSacrificable[p.type] ||
                ProjectileID.Sets.StardustDragon[p.type] || ProjectileID.Sets.MinionShot[p.type] ||
                ProjectileID.Sets.SentryShot[p.type];

        private bool HasSummonOverdrive(Projectile p)
        {
            if (p.owner < 0 || p.owner >= Main.maxPlayers || p.sentry || p.hostile || p.npcProj || p.trap ||
                ProjectileID.Sets.IsAWhip[p.type] || p.aiStyle == ProjAIStyleID.Hook ||
                !(IsSummonBodyOrShot(p) || _summonDescendant)) return false;
            Player owner = Main.player[p.owner];
            return owner.active && !owner.dead && owner.TryGetModPlayer(out SummonOverdrivePlayer state) && state.Enabled;
        }

        public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(_summonDescendant);
            bitWriter.WriteBit(IsPlusExtraUpdatesExcluded(projectile));
        }

        public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
        {
            _summonDescendant = bitReader.ReadBit();
            _plusExtraUpdatesExcluded = bitReader.ReadBit();
            TryGetArmor(projectile, out TerraArmorPlayer armor);
            UpdateExtraUpdates(projectile, armor);
        }

        public override bool PreAI(Projectile projectile)
        {
            TryGetArmor(projectile, out TerraArmorPlayer armor);
            if (projectile.type == ProjectileID.StardustGuardian && armor != null && armor.IsMode(TerraArmorMode.Summoner))
            {
                // 原版每次更新会重算召唤伤害；先加基础伤害，再应用玩家增伤，不累加到 originalDamage。
                projectile.damage = (int)armor.Player.GetTotalDamage(projectile.DamageType).ApplyTo(projectile.originalDamage + 35);
            }
            UpdateExtraUpdates(projectile, armor);
            return true;
        }

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!TryGetArmor(projectile, out TerraArmorPlayer armor)) return;
            if (projectile.type == ProjectileID.StardustGuardian &&
                armor.IsMode(TerraArmorMode.Summoner))
                modifiers.ArmorPenetration += 20;
            GlobalProjectile effects = projectile.GetGlobalProjectile<GlobalProjectile>();
            if (!effects.IsExtraHit && !effects.IsTimeEchoAttack && projectile.friendly && !projectile.hostile &&
                projectile.damage > 0 && armor.IsCloseMeleeHit(projectile.DamageType, target))
                modifiers.FinalDamage *= 1.05f;
        }

        private void UpdateExtraUpdates(Projectile projectile, TerraArmorPlayer armor)
        {
            bool enabled = armor != null && TerraArmorPlayer.Equipped(armor.Player) && projectile.friendly &&
                !projectile.hostile && projectile.damage > 0 && projectile.aiStyle != ProjAIStyleID.Hook &&
                !projectile.npcProj && !projectile.trap &&
                (!Main.projPet[projectile.type] || SummonCritPlayer.IsSummonDamage(projectile));
            // 泰拉套与御灵加速器共同持有这一份 +1；切换来源只改变维持条件，不重复加减。
            enabled |= HasSummonOverdrive(projectile);
            // 两组“+”武器及其任意派生射弹都不接收共享加成，保留自身原生更新次数。
            enabled &= !IsPlusExtraUpdatesExcluded(projectile);
            if (enabled == _extraUpdateApplied) return;
            projectile.extraUpdates = System.Math.Max(0, projectile.extraUpdates + (enabled ? 1 : -1));
            _extraUpdateApplied = enabled;
        }

        private bool IsPlusExtraUpdatesExcluded(Projectile projectile)
            => _plusExtraUpdatesExcluded || projectile.TryGetGlobalProjectile(out GlobalProjectile source) &&
                (source.PlusWeapon != PlusWeaponKind.None || source.TrackingWeapon != TrackingWeaponKind.None ||
                 source.ExcludedFromGodSmith);

        public override bool? Colliding(Projectile projectile, Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (projectile.type != ProjectileID.StardustGuardian || projectile.ai[0] != 2f ||
                !TryGetArmor(projectile, out TerraArmorPlayer armor) || !armor.IsMode(TerraArmorMode.Summoner)) return null;
            Rectangle punch = Utils.CenteredRectangle(projectile.Center + new Vector2(projectile.direction * 80, 0),
                new Vector2(160, 80));
            Rectangle body = Utils.CenteredRectangle(projectile.Center, new Vector2(projHitbox.Width * 2, projHitbox.Height * 2));
            return punch.Intersects(targetHitbox) || body.Intersects(targetHitbox);
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!TryGetArmor(projectile, out TerraArmorPlayer armor) || projectile.owner != Main.myPlayer) return;
            GlobalProjectile effects = projectile.GetGlobalProjectile<GlobalProjectile>();
            if (effects.IsExtraHit || effects.IsTimeEchoAttack) return;
            armor.RegisterMeleeHit(projectile.DamageType, target, damageDone);
            if (projectile.friendly && !projectile.hostile)
                armor.HealCloseMeleeHit(projectile.DamageType, target, damageDone);
            if (!armor.IsMode(TerraArmorMode.Ranger) || !projectile.CountsAsClass(DamageClass.Ranged) ||
                !projectile.friendly || projectile.hostile || projectile.type == ProjectileID.PhantasmArrow) return;
            // 原版幻影弓已为普通箭生成追击，避免重复；其他远程射弹补同样三箭。
            if (projectile.arrow && armor.Player.phantasmTime > 0 && damageDone > 0 && target.lifeMax > 5 &&
                projectile.aiStyle != ProjAIStyleID.SpectreWrath) return;
            SpawnPhantasm(armor.Player, target, projectile.GetSource_FromThis(), projectile.damage);
        }

        internal static void SpawnPhantasm(Player player, NPC target, IEntitySource source, int baseDamage)
        {
            Vector2 position = player.position + player.Size * Utils.RandomVector2(Main.rand, 0, 1);
            Vector2 velocity = target.DirectionFrom(position) * 6f;
            int damage = (int)(baseDamage * 0.3f);
            for (int delay = 0; delay <= 30; delay += 15)
                Projectile.NewProjectile(source, position, velocity, ProjectileID.PhantasmArrow,
                    damage, 0f, player.whoAmI, target.whoAmI, delay);
        }

        internal static bool RollSummonCrit(bool vanillaCrit, Projectile projectile, NPC target)
        {
            if (!TryGetArmor(projectile, out TerraArmorPlayer armor) || !armor.IsMode(TerraArmorMode.Summoner) ||
                !IsSummonAttack(projectile)) return vanillaCrit;
            GlobalProjectile effects = projectile.GetGlobalProjectile<GlobalProjectile>();
            if (effects.IsExtraHit || effects.IsTimeEchoAttack) return vanillaCrit;
            return Main.rand.Next(100) < GetSummonCritChance(projectile, target);
        }

        internal static int GetSummonCritChance(Projectile projectile, NPC target)
        {
            int chance = 20;
            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers &&
                Main.player[projectile.owner].TryGetModPlayer(out SummonCritPlayer crit))
                chance += (int)crit.BonusCritChance;
            if (projectile.minion || projectile.sentry || ProjectileID.Sets.MinionShot[projectile.type] ||
                ProjectileID.Sets.SentryShot[projectile.type])
            {
                if (target.HasBuff(BuffID.SwordWhipNPCDebuff)) chance += 12;
                if (target.HasBuff(BuffID.MaceWhipNPCDebuff)) chance += 10;
                foreach (var (buff, data) in WhipTagRegistry.Tags)
                    if (target.HasBuff(buff)) chance += data.CritChance;
            }
            // 渗透忍者的闪电光环暴击仍属于哨兵强化机制。
            if (projectile.type >= ProjectileID.DD2LightningAuraT1 && projectile.type <= ProjectileID.DD2LightningAuraT3)
                chance += 25;
            return System.Math.Clamp(chance, 0, 100);
        }
    }
}
