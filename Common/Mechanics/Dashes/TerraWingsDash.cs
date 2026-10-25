using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Content.Projectiles.Dashes;

namespace TestMod.Common.Mechanics.Dashes
{
    /// <summary>忍者冲刺运动与近战撞击；主动阶段免疫NPC接触，成功撞击后追加短暂免疫。</summary>
    public class TerraWingsDash : PlayerDashEffect
    {
        public const string EffectId = "TerraWingsDash";
        internal const float StartSpeed = 31f;
        internal const float SpeedRetention = 0.99f; // 每帧保留倍率；持续时间由 DashDuration 独立控制。
        // 参考灾厄 Asgardian Aegis：实际撞击命中成功后补足12tick免疫。
        internal const int ContactIFrames = 12;
        internal const float ExitSpeedRetention = 0.9f;
        // ExitSpeedRetention：冲刺结束时，仍按同方向才保留横向速度；上限为 min(StartSpeed, max(accRunSpeed, maxRunSpeed)) × 此倍率。
        // 0.35 会压到该基准的35%，产生减速顿感；1允许衔接正常跑速，但不会把已有低速抬高。
        // 松键或反向时直接清零；骑乘、抓钩等接管时跳过制动。只影响结束残速，不影响主动冲刺距离。
        public override string Id => EffectId;
        public override int DashCooldown => 20;
        public override int DashDuration => 18; // 完成最后一帧位移/命中后制动。
        public override bool EndAfterMovement => true;

        public override bool CanUseDash(Player player) => player.GetModPlayer<TerraWingsPlayer>().Equipped
            && player.active && !player.dead && !player.mount.Active && !player.CCed && !player.shimmering
            && !player.tongued && !player.pulley && player.grapCount == 0;

        public override void OnDashStart(Player player, int direction)
        {
            player.ChangeDir(direction);
            TerraWingsPlayer state = player.GetModPlayer<TerraWingsPlayer>();
            state.DashSpeed = StartSpeed;
            state.BeginDashTrail();
            // 起步不授予通用免疫；仅在 AfterMovement 确认撞击命中成功后补足。
            player.timeSinceLastDashStarted = 0;
            if (player.vortexStealthActive) player.vortexStealthActive = false;
        }

        internal static float DecaySpeed(float speed) => speed * SpeedRetention;

        public override void OnDashEffects(Player player, int direction, int verticalDirection, int frameCount)
        {
            DashPlayer dash = player.GetModPlayer<DashPlayer>();
            TerraWingsPlayer state = player.GetModPlayer<TerraWingsPlayer>();
            if (!CanUseDash(player))
            {
                dash.EndDash();
                return;
            }
            if (frameCount > 0)
                state.DashSpeed = DecaySpeed(state.DashSpeed);
            state.DashStartPosition = player.position;
            state.DashContactActive = true;
            player.velocity.X = direction * state.DashSpeed;
            // 只交给原版 DryCollision 位移，不再 position +=，避免双重移动。
            player.fallStart = player.fallStart2 = (int)(player.position.Y / 16f);
            if (!Main.dedServ)
            {
                Dust dust = Dust.NewDustDirect(player.position, player.width, player.height, DustID.TerraBlade);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }

        internal void AfterMovement(Player player)
        {
            DashPlayer dash = player.GetModPlayer<DashPlayer>();
            TerraWingsPlayer state = player.GetModPlayer<TerraWingsPlayer>();
            if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server)
            {
                Rectangle previous = new((int)state.DashStartPosition.X, (int)state.DashStartPosition.Y, player.width, player.height);
                Rectangle sweep = Rectangle.Union(previous, player.Hitbox);
                sweep.Inflate(6, 6);
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.immortal || npc.dontTakeDamage || dash.HitTargets[i]
                        || !sweep.Intersects(npc.Hitbox)) continue;
                    int damage = (int)player.GetTotalDamage(DamageClass.Melee).ApplyTo(666);
                    bool crit = Main.rand.NextFloat() * 100f < player.GetTotalCritChance(DamageClass.Melee);
                    NPC.HitInfo hit = npc.CalculateHitInfo(damage, dash.DirX, crit, 40f, DamageClass.Melee);
                    if (!DashStrikeProjectile.Strike<TerraWingsDashStrike>(player, npc, hit, EffectId)) continue;
                    dash.HitTargets[i] = true;
                    dash.HitCount++;
                    DashImmunity.Grant(player, ContactIFrames);
                }
            }
            if (!dash.IsDashing || Math.Abs(player.position.X - state.DashStartPosition.X) + 0.01f < state.DashSpeed)
                dash.EndDash();
        }

        public override void OnDashEnd(Player player)
        {
            TerraWingsPlayer state = player.GetModPlayer<TerraWingsPlayer>();
            // 清除冲刺残速；松键/反向立即停横向，同向保留少量速度，不影响垂直飞行。
            // 骑乘、抓钩或强制位移取消时不覆盖相应机制接管的速度。
            if (!player.dead && !player.mount.Active && !player.CCed && !player.shimmering
                && !player.tongued && !player.pulley && player.grapCount == 0)
            {
                bool sameDirection = player.velocity.X > 0f && player.controlRight && !player.controlLeft
                    || player.velocity.X < 0f && player.controlLeft && !player.controlRight;
                float limit = sameDirection
                    ? Math.Max(0f, Math.Min(StartSpeed, Math.Max(player.accRunSpeed, player.maxRunSpeed))) * ExitSpeedRetention
                    : 0f;
                player.velocity.X = Math.Clamp(player.velocity.X, -limit, limit);
            }
            state.DashContactActive = false;
            state.DashSpeed = 0f;
        }
    }
}
