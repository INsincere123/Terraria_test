using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Systems;
using TestMod.Content.Projectiles.TimeEcho;
using EchoProjectile = TestMod.Common.GlobalProjectiles.GlobalProjectile;
using TestMod.Common.GlobalNPCs;

namespace TestMod.Common.Players
{
    public sealed partial class TimeEchoPlayer
    {
        internal readonly TimeEchoAttackState attackState = new();
        private bool pendingAttack;
        private readonly List<(Projectile Entity, int Identity)> echoAttacks = new();
        private readonly List<TimeEchoShot> queuedShots = new(64);
        private TimeEchoMeleeFrame? meleeFrame;
        private uint meleeSwing;
        private uint meleeGeneration;
        private uint spawnedMeleeSwing;
        private bool hasSpawnedMelee;
        private Projectile meleeCarrier;
        private int meleeIdentity;
        private ulong attackBlockedTick = ulong.MaxValue;
        private ulong attackStartedTick = ulong.MaxValue;
        private uint clearedAttackGeneration;
        internal bool AcceptsAttackGeneration(uint generation) => generation > clearedAttackGeneration;
        private ulong[] echoImmunityUntil;
        private NPC[] echoImmunityTargets;
        private uint[] echoImmunityGenerations;

        internal int ReadEchoImmunity(NPC npc)
        {
            int index = npc.whoAmI;
            if (echoImmunityUntil == null || !ReferenceEquals(echoImmunityTargets[index], npc) ||
                echoImmunityGenerations[index] != npc.GetGlobalNPC<HookTargetIdentity>().Generation ||
                echoImmunityUntil[index] <= Main.GameUpdateCount) return 0;
            return (int)Math.Min(int.MaxValue, echoImmunityUntil[index] - Main.GameUpdateCount);
        }

        internal void WriteEchoImmunity(NPC npc, int ticks)
        {
            if (echoImmunityUntil == null)
            {
                if (ticks <= 0) return;
                echoImmunityUntil = new ulong[Main.maxNPCs];
                echoImmunityTargets = new NPC[Main.maxNPCs];
                echoImmunityGenerations = new uint[Main.maxNPCs];
            }
            int index = npc.whoAmI;
            echoImmunityTargets[index] = npc;
            echoImmunityGenerations[index] = npc.GetGlobalNPC<HookTargetIdentity>().Generation;
            echoImmunityUntil[index] = Main.GameUpdateCount + (ulong)Math.Max(0, ticks);
        }
        internal bool CanCopyAttack => HasAbility && HasPhantom && !Player.dead && Player.statLife > 0 &&
            attackState.ActiveTicks > 0 && attackBlockedTick != Main.GameUpdateCount && TimeEchoAttackSystem.Ready;
        internal Vector2 AttackOrigin => Phantom.Position + Player.Size * 0.5f;

        internal void QueueShot(in TimeEchoShot shot)
        {
            if (queuedShots.Count < 64) queuedShots.Add(shot);
        }

        internal void BeginMeleeSwing()
        {
            meleeSwing++;
            meleeGeneration = CanCopyAttack ? attackState.Generation : uint.MaxValue;
        }
        private void BlockQueuedAttacks()
        {
            attackBlockedTick = Main.GameUpdateCount;
            queuedShots.Clear();
            meleeFrame = null;
            pendingAttack = false;
            StopMeleeCarrier();
        }
        internal void QueueMelee(Rectangle hitbox, Item item)
        {
            if (!CanCopyAttack || Player.whoAmI != Main.myPlayer || meleeGeneration != attackState.Generation) return;
            meleeFrame = new TimeEchoMeleeFrame(hitbox, Player.Center, Main.MouseWorld,
                Player.GetWeaponDamage(item), Player.GetWeaponCrit(item), Player.GetWeaponKnockback(item, item.knockBack),
                Player.GetWeaponArmorPenetration(item), item.DamageType, item.type, Player.itemLocation - Player.Center,
                Player.GetAdjustedItemScale(item),
                Player.itemRotation, Player.direction, Player.gravDir, Player.bodyFrame,
                Player.compositeFrontArm, Player.compositeBackArm, meleeSwing, Main.GameUpdateCount);
        }

        internal void FlushEchoAttacks()
        {
            if (Player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server) return;
            if (CanCopyAttack)
            {
                foreach (TimeEchoShot shot in queuedShots)
                    if (shot.Tick == Main.GameUpdateCount && shot.Generation == attackState.Generation)
                        TimeEchoAttackSystem.CopyShot(Player, AttackOrigin, shot);
                if (meleeFrame is { } frame && frame.Tick == Main.GameUpdateCount)
                {
                    if (meleeCarrier == null || !meleeCarrier.active || meleeCarrier.identity != meleeIdentity ||
                        meleeCarrier.owner != Player.whoAmI ||
                        meleeCarrier.ModProjectile is not TimeEchoMeleeProjectile carrier || carrier.Swing != frame.Swing)
                    {
                        StopMeleeCarrier();
                        if (!hasSpawnedMelee || spawnedMeleeSwing != frame.Swing)
                        {
                            hasSpawnedMelee = true;
                            spawnedMeleeSwing = frame.Swing;
                            int index = TimeEchoAttackSystem.SpawnMelee(Player, AttackOrigin, frame, attackState.Generation);
                            meleeCarrier = index < Main.maxProjectiles ? Main.projectile[index] : null;
                            meleeIdentity = meleeCarrier?.identity ?? -1;
                        }
                    }
                    if (meleeCarrier?.ModProjectile is TimeEchoMeleeProjectile melee)
                        melee.SetFrame(AttackOrigin, frame);
                }
                else StopMeleeCarrier();
            }
            else StopMeleeCarrier();
            queuedShots.Clear();
            meleeFrame = null;
        }

        internal void RegisterEchoAttack(Projectile projectile)
        {
            // 注册与清理只遍历本玩家的副本，不扫描全局弹幕槽位。
            if (echoAttacks.Count >= Main.maxProjectiles) PruneEchoAttacks();
            if (echoAttacks.Count < Main.maxProjectiles) echoAttacks.Add((projectile, projectile.identity));
        }

        private void PruneEchoAttacks()
        {
            for (int i = echoAttacks.Count - 1; i >= 0; i--)
                if (!IsRegisteredAttack(echoAttacks[i])) echoAttacks.RemoveAt(i);
        }

        private bool IsRegisteredAttack((Projectile Entity, int Identity) entry)
            => entry.Entity.active && entry.Entity.identity == entry.Identity && entry.Entity.owner == Player.whoAmI &&
                ReferenceEquals(Main.projectile[entry.Entity.whoAmI], entry.Entity) &&
                entry.Entity.GetGlobalProjectile<EchoProjectile>().IsTimeEchoAttack;

        private void StopMeleeCarrier()
        {
            if (meleeCarrier != null && meleeCarrier.owner == Player.whoAmI && meleeCarrier.identity == meleeIdentity &&
                ReferenceEquals(Main.projectile[meleeCarrier.whoAmI], meleeCarrier) &&
                meleeCarrier.ModProjectile is TimeEchoMeleeProjectile)
                meleeCarrier.active = false;
            meleeCarrier = null;
        }

        private void ClearEchoAttacks()
        {
            clearedAttackGeneration = Math.Max(clearedAttackGeneration, attackState.Generation);
            attackState.Stop();
            pendingAttack = false;
            queuedShots.Clear();
            meleeFrame = null;
            StopMeleeCarrier();
            foreach (var entry in echoAttacks)
                if (IsRegisteredAttack(entry))
                    entry.Entity.active = false; // 不调用 Kill：不能因清理产生爆炸、分裂、掉落。
            echoAttacks.Clear();
            if (echoImmunityUntil != null)
            {
                Array.Clear(echoImmunityUntil);
                Array.Clear(echoImmunityTargets);
                Array.Clear(echoImmunityGenerations);
            }
        }

        private void TickEchoAttack()
        {
            bool owned = HasAbility;
            attackState.Tick(owned, !Player.dead && Player.statLife > 0);
            if (!owned || Player.dead) ClearEchoAttacks();
            else PruneEchoAttacks();
        }

        internal void FinishAttackTick()
        {
            // 原攻击已在玩家更新中捕获；先投送本 tick 的有效副本，再结束这一 tick 的持续期。
            if (attackStartedTick == Main.GameUpdateCount) return;
            int active = attackState.ActiveTicks;
            TickEchoAttack();
            if (active > 0 && attackState.ActiveTicks == 0 && Main.netMode == NetmodeID.Server) SendState();
        }

        private void ConsumeAttackRequest()
        {
            if (!pendingAttack) return;
            pendingAttack = false;
            if (transitionTick == Main.GameUpdateCount || !TimeEchoAttackSystem.Ready) return;
            if (attackState.TryStart(HasAbility, !Player.dead && Player.statLife > 0, HasPhantom))
            {
                attackStartedTick = Main.GameUpdateCount;
                if (Main.netMode == NetmodeID.Server) SendState();
            }
        }

        private TimeEchoMeleeProjectile GetMeleeAttack()
        {
            if (meleeCarrier?.active == true && meleeCarrier.identity == meleeIdentity && meleeCarrier.owner == Player.whoAmI &&
                meleeCarrier.ModProjectile is TimeEchoMeleeProjectile { HasFrame: true } melee)
            {
                return melee;
            }
            foreach (var entry in echoAttacks)
                if (IsRegisteredAttack(entry) && entry.Entity.ModProjectile is TimeEchoMeleeProjectile remote && remote.HasFrame)
                {
                    return remote;
                }
            return null;
        }

        internal void DrawEchoWeapon() => GetMeleeAttack()?.DrawWeapon(VisualPosition + Player.Size * 0.5f, VisualOpacity);

        internal bool TryGetAttackPose(out TimeEchoMeleeFrame frame, out float rotation)
        {
            if (GetMeleeAttack() is { } melee)
            {
                frame = melee.Frame;
                rotation = melee.PoseRotation;
                return true;
            }
            frame = default;
            rotation = 0;
            return false;
        }
    }
}
