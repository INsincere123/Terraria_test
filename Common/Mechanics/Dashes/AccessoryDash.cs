using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TestMod.Common.Players;
using TestMod.Content.Items.DamageTypes;
using TestMod.Content.Projectiles.Dashes;

namespace TestMod.Common.Mechanics.Dashes
{
    /// <summary>参数化的饰品冲刺：原版碰撞位移、位移后撞击与独立接触免疫。</summary>
    public sealed class AccessoryDash : PlayerDashEffect
    {
        public const string EffectId = "AccessoryDash";
        public override string Id => EffectId;
        public override bool EndAfterMovement => true;
        private static AccessoryDashPlayer State(Player player) => player.GetModPlayer<AccessoryDashPlayer>();
        public override int GetMaxDuration(Player player) => State(player).Selected.Motion == AccessoryDashMotion.FixedDuration
            ? State(player).Selected.Duration : int.MaxValue / 2;
        public override int GetCooldown(Player player) => State(player).Configuration.Cooldown;

        public override bool CanUseDash(Player player)
        {
            var state = State(player);
            var config = player.GetModPlayer<DashPlayer>().ActiveEffect == this ? state.Configuration : state.Selected;
            return config != null && state.SourceEquipped(config.Source) && player.active && !player.dead
                && !player.mount.Active && !player.CCed && !player.shimmering && !player.tongued
                && !player.pulley && player.grapCount == 0;
        }

        public override void OnDashStart(Player player, int direction)
        {
            var state = State(player);
            state.Configuration = state.Selected;
            state.Started = state.MovementPending = state.EndPending = false;
            state.Speed = state.Configuration.StartSpeed;
            player.ChangeDir(direction);
            player.timeSinceLastDashStarted = 0;
            if (state.Configuration.Source == AccessoryDashSource.TerraWings)
            {
                player.GetModPlayer<TerraWingsPlayer>().BeginDashTrail();
                player.vortexStealthActive = false;
            }
            else player.GetModPlayer<CompositeAccessoryPlayer>().BeginDashTrail();
        }

        public override void OnDashEffects(Player player, int direction, int verticalDirection, int frameCount)
        {
            var state = State(player);
            if (!CanUseDash(player)) { player.GetModPlayer<DashPlayer>().EndDash(); return; }
            var config = state.Configuration;
            if (config.Motion != AccessoryDashMotion.FixedDuration) return;
            if (frameCount > 0) state.Speed *= config.FastRetention;
            state.StartPosition = player.position;
            state.MovementPending = state.Started = true;
            player.velocity.X = direction * state.Speed;
            player.fallStart = player.fallStart2 = (int)(player.position.Y / 16f);
            if (config.Visual == AccessoryDashVisual.Terra && !Main.dedServ)
            {
                Dust dust = Dust.NewDustDirect(player.position, player.width, player.height, DustID.TerraBlade);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }

        // 仅分段运动接管 DashMovement，保留实际当前速度、地空加速和反向输入。
        internal void Move(Player player)
        {
            var state = State(player);
            var config = state.Configuration;
            DashPlayer dash = player.GetModPlayer<DashPlayer>();
            if (!CanUseDash(player)) { dash.EndDash(); return; }
            state.StartPosition = player.position;
            state.MovementPending = true;
            if (!state.Started)
            {
                state.Started = true;
                player.velocity.X = config.StartSpeed * dash.DirX;
                Point top = (player.Center + new Vector2(dash.DirX * player.width / 2 + 2,
                    player.gravDir * -player.height / 2f + player.gravDir * 2f)).ToTileCoordinates();
                Point middle = (player.Center + new Vector2(dash.DirX * player.width / 2 + 2, 0)).ToTileCoordinates();
                if (WorldGen.SolidOrSlopedTile(top.X, top.Y) || WorldGen.SolidOrSlopedTile(middle.X, middle.Y))
                    player.velocity.X /= 2f;
                player.dashDelay = -1;
                return;
            }
            player.StopVanityActions();
            player.vortexStealthActive = false;
            float runSpeed = Math.Max(player.accRunSpeed, player.maxRunSpeed);
            player.doorHelper.AllowOpeningDoorsByVelocityAloneForATime(config.Cooldown * 3);
            if (Math.Abs(player.velocity.X) > config.FastThreshold) player.velocity.X *= config.FastRetention;
            else if (Math.Abs(player.velocity.X) > runSpeed) player.velocity.X *= config.SlowRetention;
            else
            {
                player.velocity.X = Math.Sign(player.velocity.X) * runSpeed;
                state.EndPending = true; // 先完成这一帧碰撞与撞击，再结束。
            }
            if (config.Visual == AccessoryDashVisual.Smoke && !Main.dedServ)
                for (int i = 0; i < 2; i++)
                {
                    Dust dust = Dust.NewDustDirect(player.position + new Vector2(0, player.velocity.Y != 0 ? player.height / 2 - 8 : player.height - 4),
                        player.width, player.velocity.Y != 0 ? 16 : 8, DustID.Smoke, 0, 0, 100, default, 1.4f);
                    dust.velocity *= 0.1f;
                }
        }

        internal void AfterMovement(Player player)
        {
            var state = State(player);
            DashPlayer dash = player.GetModPlayer<DashPlayer>();
            if (!CanUseDash(player)) { dash.EndDash(); return; }
            if (!state.MovementPending) return;
            state.MovementPending = false;
            var config = state.Configuration;
            if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server)
            {
                Rectangle previous = new((int)state.StartPosition.X, (int)state.StartPosition.Y, player.width, player.height);
                Rectangle sweep = Rectangle.Union(previous, player.Hitbox);
                sweep.Inflate(config.HitboxExpand, config.HitboxExpand);
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.immortal || npc.dontTakeDamage || dash.HitTargets[i]
                        || !sweep.Intersects(npc.Hitbox)) continue;
                    int damage = (int)player.GetTotalDamage(TrueDamageClass.Instance).ApplyTo(config.Damage);
                    bool crit = Main.rand.NextFloat() * 100f < player.GetTotalCritChance(TrueDamageClass.Instance);
                    NPC.HitModifiers modifiers = npc.GetIncomingStrikeModifiers(TrueDamageClass.Instance, dash.DirX);
                    modifiers.ScalingArmorPenetration += 1f;
                    NPC.HitInfo hit = modifiers.ToHitInfo(damage, crit, config.Knockback);
                    if (!DashStrikeProjectile.Strike<AccessoryDashStrike>(player, npc, hit, Id)) continue;
                    dash.HitTargets[i] = true;
                    dash.HitCount++;
                    state.GrantContactImmunity(config.ContactImmunity);
                    // 命中回调可能取消或替换冲刺；不能继续以旧来源结算后续目标。
                    if (dash.ActiveEffect != this) return;
                }
            }
            bool stopped = config.Motion == AccessoryDashMotion.FixedDuration
                ? Math.Abs(player.position.X - state.StartPosition.X) + 0.01f < state.Speed
                : state.Started && player.velocity.X == 0f;
            if (state.EndPending || !dash.IsDashing || stopped) dash.EndDash();
        }

        public override void OnDashEnd(Player player)
        {
            var state = State(player);
            var config = state.Configuration;
            if (config.Motion == AccessoryDashMotion.FixedDuration && !player.dead && !player.mount.Active
                && !player.CCed && !player.shimmering && !player.tongued && !player.pulley && player.grapCount == 0)
            {
                bool sameDirection = player.velocity.X > 0f && player.controlRight && !player.controlLeft
                    || player.velocity.X < 0f && player.controlLeft && !player.controlRight;
                float limit = sameDirection
                    ? Math.Max(0f, Math.Min(config.StartSpeed, Math.Max(player.accRunSpeed, player.maxRunSpeed))) * config.ExitRetention : 0f;
                player.velocity.X = Math.Clamp(player.velocity.X, -limit, limit);
            }
            if (config.Motion == AccessoryDashMotion.Segmented) player.dash = player.dashDelay = 0;
            state.Started = state.MovementPending = state.EndPending = false;
            state.Speed = 0f;
            // Configuration 留到下次启动，供驱动在 OnDashEnd 后读取本次冷却。
        }
    }
}
