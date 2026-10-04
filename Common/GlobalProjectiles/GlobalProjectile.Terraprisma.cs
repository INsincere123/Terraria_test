using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        private TerraprismaFlightController _terraprismaFlight;
        private int _terraprismaTargetType;
        private NPC _terraprismaTargetEntity;
        private int _terraprismaPreviousMaxUpdates;

        private static void ApplyTerraprismaDefaults(Projectile projectile)
        {
            if (projectile.type != ProjectileID.EmpressBlade) return;
            projectile.MaxUpdates = 4;
            projectile.localNPCHitCooldown = TerraprismaFlightController.HitCooldownFrames * projectile.MaxUpdates;
        }

        private void ApplyTerraprismaAI(Projectile projectile, Player player)
        {
            bool authority = projectile.owner == Main.myPlayer;
            int updates = Math.Max(1, projectile.MaxUpdates);
            float frameStep = 1f / updates;
            UpdateTerraprismaTiming(projectile, updates);
            if (player.dead) player.empressBlade = false;
            if (player.empressBlade) projectile.timeLeft = 2;
            projectile.friendly = !player.dead && player.empressBlade;
            projectile.hostile = false;
            projectile.tileCollide = false;

            GetTerraprismaIdlePosition(projectile, player, out Vector2 idlePosition, out float idleRotation);
            bool returning = !projectile.friendly;
            bool changed = false;
            if (Vector2.DistanceSquared(projectile.Center, player.Center)
                > TerraprismaFlightController.ReturnRange * TerraprismaFlightController.ReturnRange)
            {
                returning = true;
                if (authority)
                {
                    projectile.Center = idlePosition;
                    projectile.velocity = Vector2.Zero;
                    _trackingTargetCache = default;
                    projectile.netUpdate = true;
                }
            }

            int oldIndex = (int)projectile.ai[1] - 1;
            if (authority)
            {
                int index = -1;
                if (!returning)
                {
                    index = _trackingTargetCache.FindNearest(projectile, projectile.Center,
                        TerraprismaFlightController.TargetRange, out bool replaced, prioritizeMinionTarget: true);
                    changed |= replaced;
                }
                NPC entity = index >= 0 ? Main.npc[index] : null;
                int type = entity?.type ?? 0;
                changed |= index != oldIndex || type != _terraprismaTargetType
                    || !ReferenceEquals(entity, _terraprismaTargetEntity);
                projectile.ai[1] = index + 1;
                _terraprismaTargetType = type;
                _terraprismaTargetEntity = entity;
                if (changed)
                {
                    if (entity != null) _terraprismaFlight.Start(projectile.velocity, entity.Center - projectile.Center);
                    else _terraprismaFlight.Reset();
                }
            }

            NPC target = returning ? null : ResolveTerraprismaTarget(projectile);
            if (authority)
            {
                float phase = target == null ? 0f : 1f;
                if (projectile.ai[0] != phase) { changed = true; projectile.ai[0] = phase; }
            }
            if (target != null && projectile.ai[0] == 1f)
            {
                Vector2 separation = GetTerraprismaSeparation(projectile);
                projectile.velocity = _terraprismaFlight.Update(projectile.Center, projectile.velocity,
                    target.Center, frameStep, separation);
                Vector2 offset = target.Center - projectile.Center;
                if (offset.LengthSquared() > 0.0001f)
                    projectile.rotation = TerraprismaFlightController.Rotate(projectile.rotation,
                        offset.ToRotation() + MathHelper.PiOver2, frameStep);
            }
            else
            {
                projectile.velocity = TerraprismaFlightController.Return(projectile.Center, projectile.velocity,
                    idlePosition, player.velocity, frameStep);
                projectile.rotation = TerraprismaFlightController.Rotate(projectile.rotation, idleRotation, frameStep);
            }
            if (Math.Abs(projectile.velocity.X) > 0.01f)
                projectile.direction = projectile.spriteDirection = projectile.velocity.X > 0f ? 1 : -1;
            MarkTrackingActivity(projectile, target != null && projectile.ai[0] == 1f, changed);
            if (authority && changed) projectile.netUpdate = true;
        }

        private void UpdateTerraprismaTiming(Projectile projectile, int updates)
        {
            int previous = _terraprismaPreviousMaxUpdates;
            if (previous != updates)
            {
                if (previous > 0) projectile.velocity *= (float)previous / updates;
                for (int i = 0; i < projectile.localNPCImmunity.Length; i++)
                {
                    int remaining = projectile.localNPCImmunity[i];
                    if (previous > 0 && remaining > 0)
                        projectile.localNPCImmunity[i] = (int)Math.Ceiling((double)remaining * updates / previous);
                    // 首次接管解除原版每轮命中后的 -1 标记，以后不按阶段或目标切换清空免疫。
                    else if (previous == 0 && remaining < 0) projectile.localNPCImmunity[i] = 0;
                }
                _terraprismaPreviousMaxUpdates = updates;
            }
            // Update 在 AI 之前递减免疫；只换算计时单位，不重复递减。
            projectile.localNPCHitCooldown = TerraprismaFlightController.HitCooldownFrames * updates;
        }

        private NPC ResolveTerraprismaTarget(Projectile projectile)
        {
            int index = (int)projectile.ai[1] - 1;
            if (index < 0 || index >= Main.maxNPCs) return null;
            NPC npc = Main.npc[index];
            return ReferenceEquals(npc, _terraprismaTargetEntity) && npc.type == _terraprismaTargetType
                && npc.CanBeChasedBy(projectile) && Vector2.DistanceSquared(projectile.Center, npc.Center)
                    < TerraprismaFlightController.TargetRange * TerraprismaFlightController.TargetRange ? npc : null;
        }

        private static Vector2 GetTerraprismaSeparation(Projectile projectile)
        {
            Vector2 force = Vector2.Zero;
            foreach (Projectile other in ProjectileLookup.Owned(projectile.owner, projectile.type))
            {
                if (other.whoAmI == projectile.whoAmI) continue;
                Vector2 delta = projectile.Center - other.Center;
                float distanceSq = delta.LengthSquared();
                if (distanceSq >= 24f * 24f) continue;
                force += distanceSq > 0f ? delta * (0.1f / MathF.Sqrt(distanceSq))
                    : new Vector2(projectile.whoAmI > other.whoAmI ? 0.1f : -0.1f, 0f);
            }
            return force;
        }

        private static void GetTerraprismaIdlePosition(Projectile projectile, Player player,
            out Vector2 idlePosition, out float idleRotation)
        {
            int index = 0, total = 0;
            foreach (Projectile other in ProjectileLookup.Owned(projectile.owner, projectile.type))
            {
                if (other.whoAmI < projectile.whoAmI) index++;
                total++;
            }
            // 沿用原版 AI_156_GetIdlePosition 的队列位置、重力方向和浮动相位。
            int order = index + 1;
            idleRotation = MathHelper.WrapAngle(order * MathHelper.TwoPi / 60f * player.direction + MathHelper.PiOver2);
            int phase = order % Math.Max(1, total);
            float angle = (player.miscCounterNormalized * (2f + phase) + phase * 0.5f + player.direction * 1.3f)
                * MathHelper.TwoPi;
            Vector2 bob = new Vector2(0f, 0.5f).RotatedBy(angle) * 4f;
            idlePosition = idleRotation.ToRotationVector2() * 10f + player.MountedCenter
                + new Vector2(player.direction * (order * -6 - 16), player.gravDir * -15f) + bob;
            idleRotation += MathHelper.PiOver2;
        }

        private void WriteTerraprismaAI(BinaryWriter writer)
        {
            writer.Write(_terraprismaTargetType);
            _terraprismaFlight.Write(writer);
        }

        private void ReadTerraprismaAI(Projectile projectile, BinaryReader reader)
        {
            _terraprismaTargetType = reader.ReadInt32();
            int index = (int)projectile.ai[1] - 1;
            _terraprismaTargetEntity = index >= 0 && index < Main.maxNPCs ? Main.npc[index] : null;
            _terraprismaFlight.Read(reader, projectile.ai[0] == 1f && _terraprismaTargetEntity != null);
        }
    }
}
