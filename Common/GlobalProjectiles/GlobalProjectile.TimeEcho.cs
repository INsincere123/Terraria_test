using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        public bool IsTimeEchoAttack { get; private set; }
        private TimeEchoAttackIdentity echoIdentity;
        private bool echoRegistered;

        private bool TimeEcho_OnSpawn(Projectile p, IEntitySource source)
        {
            if (source is TimeEchoAttackSource attack)
            {
                IsTimeEchoAttack = true;
                echoIdentity = attack.Identity;
                p.DamageType = attack.DamageClass;
                p.CritChance = attack.CritChance;
                p.ArmorPenetration = attack.ArmorPenetration;
                if (attack.Shot is { } shot) shot.ApplyParameters(p);
                InitializeEchoOwner(p, attack.Shot?.OwnerPose ?? default, attack.OwnerOrigin ?? p.Center, attack.Shot?.Entity);
            }
            else if (source is EntitySource_Parent { Entity: Projectile parent } &&
                parent.GetGlobalProjectile<GlobalProjectile>() is { IsTimeEchoAttack: true } root)
            {
                IsTimeEchoAttack = true;
                echoIdentity = root.echoIdentity;
                if (!TimeEchoAttackSystem.CanCopyChild(parent, p, source))
                {
                    p.active = false;
                    return true;
                }
                p.DamageType = parent.DamageType;
                p.CritChance = parent.CritChance;
                p.ArmorPenetration = parent.ArmorPenetration;
                InitializeEchoOwner(p, root.echoOwnerPose, root.echoOwnerOrigin);
            }
            if (!IsTimeEchoAttack)
            {
                TimeEchoAttackSystem.ObserveSpawn(p, source);
                return false;
            }
            if (TimeEchoAttackSystem.IsExcludedProjectile(p))
            {
                p.active = false;
                return true;
            }
            ConfigureEcho(p);
            return true;
        }

        private void ConfigureEcho(Projectile p)
        {
            p.friendly = true;
            p.hostile = false;
            p.npcProj = true;
            p.noEnchantments = p.noEnchantmentVisuals = p.noDropItem = true;
            // 原生 local immunity 保持 AI 更新间隔；原生 owner immunity 由独立世界 tick 表接管。
            p.usesIDStaticNPCImmunity = false;
            p.appliesImmunityTimeOnSingleHits = false;
            IsExtraHit = IsHomingTagged = subhandDynamicText = false;
            if (!echoRegistered && p.owner >= 0 && p.owner < Main.maxPlayers &&
                Main.player[p.owner].TryGetModPlayer(out TimeEchoPlayer state))
            {
                state.RegisterEchoAttack(p);
                echoRegistered = true;
            }
        }

        private bool TimeEcho_PreAI(Projectile p)
        {
            if (TimeEchoAttackSystem.IsExcludedProjectile(p) ||
                (TimeEchoOwnerPose.NeedsProxy(p) && echoOwnerPose.Animation <= 0) ||
                p.owner < 0 || p.owner >= Main.maxPlayers || !Main.player[p.owner].active ||
                Main.player[p.owner].dead || !Main.player[p.owner].TryGetModPlayer(out TimeEchoPlayer echo) ||
                !echo.HasAbility || !echo.AcceptsAttackGeneration(echoIdentity.Generation))
            {
                p.active = false;
                return false;
            }
            return UpdateEchoControl(p, echo);
        }

        private void TimeEcho_PostAI(Projectile p)
        {
            if (TimeEchoAttackSystem.IsExcludedProjectile(p)) { p.active = false; return; }
            // AI 可以控制自身是否可命中，但不能将副本改回敌方或恢复玩家命中路径。
            p.hostile = false;
            p.npcProj = true;
            p.noEnchantments = p.noEnchantmentVisuals = p.noDropItem = true;
        }

        private void TimeEcho_Write(Projectile p, BinaryWriter writer)
        {
            writer.Write(echoIdentity.Root); writer.Write(echoIdentity.Generation);
            writer.Write(p.DamageType.Type); writer.Write(p.CritChance); writer.Write(p.ArmorPenetration);
            writer.Write(p.usesLocalNPCImmunity);
            writer.Write(p.localNPCHitCooldown);
            writer.Write(p.originalDamage); writer.Write(p.scale);
            writer.Write(p.penetrate); writer.Write(p.maxPenetrate); writer.Write(p.timeLeft);
            writer.Write(p.extraUpdates); writer.Write(p.tileCollide); writer.Write(p.ignoreWater);
            WriteEchoOwner(writer);
        }

        private void TimeEcho_Read(Projectile p, BinaryReader reader)
        {
            echoIdentity = new(reader.ReadUInt32(), reader.ReadUInt32());
            p.DamageType = DamageClassLoader.GetDamageClass(reader.ReadInt32()) ?? p.DamageType;
            p.CritChance = reader.ReadInt32(); p.ArmorPenetration = reader.ReadInt32();
            p.usesLocalNPCImmunity = reader.ReadBoolean();
            int cooldown = reader.ReadInt32();
            p.originalDamage = reader.ReadInt32(); p.scale = reader.ReadSingle();
            p.penetrate = reader.ReadInt32(); p.maxPenetrate = reader.ReadInt32(); p.timeLeft = reader.ReadInt32();
            p.extraUpdates = reader.ReadInt32(); p.tileCollide = reader.ReadBoolean(); p.ignoreWater = reader.ReadBoolean();
            ReadEchoOwner(p, reader);
            ConfigureEcho(p);
            p.localNPCHitCooldown = cooldown;
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if (!IsTimeEchoAttack) return null;
            if (target.friendly) return false;
            if (target.type != Terraria.ID.NPCID.TargetDummy) return null;
            // 原版 npcProj 跳过 immortal 假人；显式允许时仍保留原本的 owner 免疫门槛。
            if ((projectile.maxPenetrate != 1 || projectile.usesLocalNPCImmunity) &&
                Main.player[projectile.owner].TryGetModPlayer(out TimeEchoPlayer echo) && echo.ReadEchoImmunity(target) > 0)
                return false;
            return true;
        }
        public override bool CanHitPvp(Projectile projectile, Player target) => !IsTimeEchoAttack;
        public override bool CanHitPlayer(Projectile projectile, Player target) => !IsTimeEchoAttack;
        public override Color? GetAlpha(Projectile projectile, Color lightColor)
            => IsTimeEchoAttack ? Color.Lerp(TimeEchoSystem.EchoBlue, TimeEchoSystem.EchoPurple, 0.4f) * 0.7f : null;

        private void TimeEcho_ModifyHit(Projectile p, ref NPC.HitModifiers modifiers)
        {
            // 始终保留原始伤害，水晶碎片等从父弹幕 damage 继承后仍只在结算处减半一次。
            modifiers.SourceDamage *= 0.5f;
            if (p.DamageType.UseStandardCritCalcs && Main.rand.Next(100) < Math.Clamp(p.CritChance, 0, 100)) modifiers.SetCrit();
            else modifiers.DisableCrit();
        }
    }
}
