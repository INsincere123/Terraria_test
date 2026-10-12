using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Mechanics.Dashes
{
    // 运行字段放在玩家实例；仅替换本冲刺的 DashMovement，不复制原版撞击分支。
    public abstract class CompositePhaseDash : PlayerDashEffect
    {
        protected abstract bool Ninja { get; }
        public override int DashDuration => int.MaxValue / 2;
        public override bool EndAfterMovement => true;
        public override int DashCooldown => Ninja ? 20 : 30;

        public override bool CanUseDash(Player player)
        {
            var state = player.GetModPlayer<CompositeAccessoryPlayer>();
            return (Ninja ? state.VeilEquipped : state.ShieldEquipped) && player.active && !player.dead
                && !player.mount.Active && !player.CCed && !player.shimmering && !player.tongued
                && !player.pulley && player.grapCount == 0;
        }

        public override void OnDashStart(Player player, int direction)
        {
            player.ChangeDir(direction);
            var state = player.GetModPlayer<CompositeAccessoryPlayer>();
            state.DashStarted = false;
            state.BeginDashTrail();
            player.timeSinceLastDashStarted = 0;
        }

        // 与原版同一更新阶段读取当前速度，保留地面/空中加速、反向输入与碰撞结果。
        internal void Move(Player player)
        {
            DashPlayer dash = player.GetModPlayer<DashPlayer>();
            if (!CanUseDash(player)) { dash.EndDash(); return; }
            var state = player.GetModPlayer<CompositeAccessoryPlayer>();
            if (!state.DashStarted)
            {
                state.DashStarted = true;
                player.velocity.X = (Ninja ? 16.9f : 14.5f) * dash.DirX;
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
            player.doorHelper.AllowOpeningDoorsByVelocityAloneForATime(DashCooldown * 3);
            if (Math.Abs(player.velocity.X) > 12f) player.velocity.X *= Ninja ? 0.992f : 0.985f;
            else if (Math.Abs(player.velocity.X) > runSpeed) player.velocity.X *= Ninja ? 0.96f : 0.94f;
            else
            {
                player.velocity.X = Math.Sign(player.velocity.X) * runSpeed;
                dash.EndDash();
            }
            if (Ninja && !Main.dedServ)
                for (int i = 0; i < 2; i++)
                {
                    Dust dust = Dust.NewDustDirect(player.position + new Vector2(0, player.velocity.Y != 0 ? player.height / 2 - 8 : player.height - 4),
                        player.width, player.velocity.Y != 0 ? 16 : 8, DustID.Smoke, 0, 0, 100, default, 1.4f);
                    dust.velocity *= 0.1f;
                }
        }

        public override void OnDashEffects(Player player, int direction, int verticalDirection, int frameCount)
        {
            if (!CanUseDash(player)) player.GetModPlayer<DashPlayer>().EndDash();
        }

        public override void OnDashEnd(Player player)
        {
            player.GetModPlayer<CompositeAccessoryPlayer>().DashStarted = false;
            player.dash = player.dashDelay = 0;
        }
    }

    public sealed class CthulhuPhaseDash : CompositePhaseDash
    {
        internal const string EffectId = "CthulhuPhaseDash";
        public override string Id => EffectId;
        protected override bool Ninja => false;
    }

    public sealed class VeilPhaseDash : CompositePhaseDash
    {
        internal const string EffectId = "VeilPhaseDash";
        public override string Id => EffectId;
        protected override bool Ninja => true;
    }
}
