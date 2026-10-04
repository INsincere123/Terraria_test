using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using TestMod.Common.Utilities;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        private int _abigailTargetIndex = -1;
        private int _abigailTargetType;
        private NPC _abigailTargetEntity;
        private float _abigailAnimationTime;
        private bool _abigailWasAttacking;
        private int _abigailPreviousMaxUpdates;

        private void ApplyAbigailAI(Projectile projectile, Player player)
        {
            bool authority = projectile.owner == Main.myPlayer;
            int updates = Math.Max(1, projectile.MaxUpdates);
            float frameStep = 1f / updates;
            if (_abigailPreviousMaxUpdates > 0 && _abigailPreviousMaxUpdates != updates)
                projectile.velocity *= (float)_abigailPreviousMaxUpdates / updates;
            _abigailPreviousMaxUpdates = updates;

            // 保留原版维持与计数弹幕成长，不生成额外伤害弹幕，也不接管命中冷却。
            // 当前引擎在 Projectile.Update -> AI 之前递减 localNPCImmunity，不能再次手动递减。
            if (player.dead) player.abigailMinion = false;
            if (player.abigailMinion) projectile.timeLeft = 2;
            projectile.originalDamage = player.highestAbigailCounterOriginalDamage;
            projectile.tileCollide = false;
            projectile.hostile = false;
            projectile.friendly = !player.dead && player.abigailMinion;

            Vector2 followPosition = player.Center + new Vector2(-40f * player.direction, -20f);
            bool changed = false;
            bool returning = !projectile.friendly;
            if (Vector2.DistanceSquared(projectile.Center, player.Center)
                > AbigailTrackingController.ReturnRange * AbigailTrackingController.ReturnRange)
            {
                returning = true;
                if (authority)
                {
                    projectile.Center = followPosition;
                    projectile.velocity = Vector2.Zero;
                    _trackingTargetCache = default;
                    projectile.netUpdate = true;
                }
            }

            if (authority)
            {
                int index = -1;
                if (!returning)
                {
                    int preferred = player.HasMinionAttackTargetNPC ? player.MinionAttackTargetNPC : -1;
                    // 公共缓存允许其他召唤物的指定目标穿墙；阿比盖尔在传入前单独检查视线。
                    if (!IsAbigailTargetValid(projectile, preferred)) preferred = -1;
                    index = _trackingTargetCache.FindNearest(projectile, projectile.Center,
                        AbigailTrackingController.TargetRange, out bool replaced, retargetFrames: 30,
                        preferredTarget: preferred, requireLineOfSight: true);
                    changed |= replaced;
                }
                changed |= SetAbigailTarget(index);
            }

            NPC target = !returning && IsAbigailTargetValid(projectile, _abigailTargetIndex)
                && ReferenceEquals(Main.npc[_abigailTargetIndex], _abigailTargetEntity)
                && Main.npc[_abigailTargetIndex].type == _abigailTargetType
                ? Main.npc[_abigailTargetIndex] : null;
            int extraSlots = Math.Max(0, player.ownedProjectileCounts[ProjectileID.AbigailCounter] - 1);
            float duration = AbigailTrackingController.AttackDuration(extraSlots);
            float boundarySq = target != null
                ? AbigailTrackingController.BoundaryDistanceSquared(projectile.Center, target.Hitbox)
                : float.PositiveInfinity;
            projectile.ai[0] = AbigailTrackingController.UpdateState(projectile.ai[0], boundarySq,
                target != null, duration, frameStep, authority, out bool phaseChanged, out bool attackStarted);
            changed |= phaseChanged;

            bool attacking = projectile.ai[0] >= 2f;
            projectile.velocity = AbigailTrackingController.Move(projectile.Center, projectile.velocity,
                target?.Center ?? followPosition, target?.velocity ?? player.velocity,
                AbigailTrackingController.ChaseSpeed(extraSlots), frameStep,
                attacking && target != null, target == null);
            UpdateAbigailAnimation(projectile, duration, frameStep, attacking, attackStarted);
            projectile.rotation = projectile.velocity.X * updates * 0.05f;
            if (Math.Abs(projectile.velocity.X) > 0.01f)
                projectile.direction = projectile.spriteDirection = projectile.velocity.X > 0f ? 1 : -1;
            else if (target == null) projectile.direction = projectile.spriteDirection = player.direction;

            MarkTrackingActivity(projectile, target != null, changed);
            if (authority && phaseChanged) projectile.netUpdate = true;
        }

        private static bool IsAbigailTargetValid(Projectile projectile, int index)
        {
            if (index < 0 || index >= Main.maxNPCs) return false;
            NPC target = Main.npc[index];
            return target.CanBeChasedBy(projectile)
                && Vector2.DistanceSquared(projectile.Center, target.Center)
                    < AbigailTrackingController.TargetRange * AbigailTrackingController.TargetRange
                && Collision.CanHit(projectile.Center, 0, 0, target.Center, 0, 0);
        }

        private bool SetAbigailTarget(int index)
        {
            NPC target = index >= 0 ? Main.npc[index] : null;
            int type = target?.type ?? 0;
            bool changed = index != _abigailTargetIndex || type != _abigailTargetType
                || !ReferenceEquals(target, _abigailTargetEntity);
            _abigailTargetIndex = index;
            _abigailTargetType = type;
            _abigailTargetEntity = target;
            return changed;
        }

        private void UpdateAbigailAnimation(Projectile projectile, float duration, float frameStep,
            bool attacking, bool attackStarted)
        {
            bool enteredAttack = attacking && !_abigailWasAttacking;
            if (attacking != _abigailWasAttacking) _abigailAnimationTime = 0f;
            _abigailWasAttacking = attacking;
            if (attacking)
            {
                // 原版攻击帧 8..12；攻击时 frameCounter 每次 AI 累加两次。
                _abigailAnimationTime += (enteredAttack ? 1f : 2f) * frameStep;
                int frame = (int)(_abigailAnimationTime / 12f);
                if (frame > 6) { _abigailAnimationTime = 0f; frame = 5; }
                projectile.frame = frame <= 2 ? 8 + frame : frame == 3 || frame == 5 ? 11 : 12;
                projectile.localAI[1] = projectile.ai[0] / duration;
            }
            else
            {
                _abigailAnimationTime += frameStep;
                if (_abigailAnimationTime >= 56f) _abigailAnimationTime %= 56f;
                projectile.frame = (int)(_abigailAnimationTime / 7f);
                if (projectile.localAI[1] <= 0f) projectile.localAI[1] = -1f;
                else
                {
                    projectile.localAI[1] = MathHelper.Clamp(projectile.localAI[1] + 0.05f * frameStep, 0f, 1f);
                    if (projectile.localAI[1] >= 1f) projectile.localAI[1] = -1f;
                }
            }
            projectile.frameCounter = (int)_abigailAnimationTime;

            if (Main.netMode == NetmodeID.Server) return;
            if (attacking && (attackStarted || enteredAttack))
                SoundEngine.PlaySound(SoundID.AbigailAttack, projectile.Center);
            // 每游戏帧一次，避免 extraUpdates 增加原版粒子密度和闲置音效频率。
            if (projectile.numUpdates != -1) return;
            if (attacking && Main.rand.NextBool(2))
            {
                float radius = 1.1f + Main.rand.NextFloat() * 0.3f;
                float scale = 1.4f + Main.rand.NextFloat() * 0.4f;
                Vector2 edge = Main.rand.NextVector2CircularEdge(projectile.width * radius,
                    -projectile.height * 0.25f * radius);
                Dust dust = Dust.NewDustDirect(projectile.Bottom + edge, 1, 1, DustID.SteampunkSteam,
                    0f, 0f, 50, Color.GhostWhite, scale);
                dust.velocity = edge * 0.0125f + (edge.ToRotation() + MathHelper.PiOver2).ToRotationVector2();
                dust.noGravity = true;
            }
            if (projectile.velocity.LengthSquared() * projectile.MaxUpdates * projectile.MaxUpdates > 0.01f
                && Main.rand.NextBool(1500))
                SoundEngine.PlaySound(SoundID.AbigailCry, projectile.Center);
        }

        private void WriteAbigailAI(Projectile projectile, BinaryWriter writer)
        {
            writer.Write((short)_abigailTargetIndex);
            writer.Write(_abigailTargetType);
            writer.Write(_abigailAnimationTime);
            writer.Write((byte)projectile.frame);
            writer.Write(projectile.localAI[1]);
        }

        private void ReadAbigailAI(Projectile projectile, BinaryReader reader)
        {
            int index = reader.ReadInt16();
            int type = reader.ReadInt32();
            float animationTime = reader.ReadSingle();
            int frame = reader.ReadByte();
            float animationProgress = reader.ReadSingle();
            SetAbigailTarget(index >= 0 && index < Main.maxNPCs ? index : -1);
            _abigailTargetType = type;
            _abigailAnimationTime = float.IsFinite(animationTime) ? MathHelper.Clamp(animationTime, 0f, 84f) : 0f;
            projectile.frame = Math.Clamp(frame, 0, 12);
            projectile.frameCounter = (int)_abigailAnimationTime;
            projectile.localAI[1] = float.IsFinite(animationProgress) ? MathHelper.Clamp(animationProgress, -1f, 1f) : -1f;
            _abigailWasAttacking = projectile.ai[0] >= 2f;
            _abigailPreviousMaxUpdates = Math.Max(1, projectile.MaxUpdates);
        }
    }
}
