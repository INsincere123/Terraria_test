using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.Dashes;

namespace TestMod.Common.Players
{
    public sealed class AccessoryDashPlayer : ModPlayer
    {
        internal AccessoryDashConfig Selected, Configuration;
        internal bool Started, MovementPending, EndPending;
        internal float Speed;
        internal Vector2 StartPosition;
        internal int ContactImmunityTicks { get; private set; }
        private ulong lastImmunityTick = ulong.MaxValue;

        public override void ResetEffects() => Selected = null;

        internal void Offer(AccessoryDashConfig config)
        {
            if (Selected == null || config.Source > Selected.Source) Selected = config;
        }

        internal bool IsSourceActive(AccessoryDashSource source) =>
            Player.GetModPlayer<DashPlayer>().ActiveEffect is AccessoryDash && Configuration?.Source == source;

        internal bool SourceEquipped(AccessoryDashSource source) => source switch
        {
            AccessoryDashSource.Cthulhu => Player.GetModPlayer<CompositeAccessoryPlayer>().ShieldEquipped,
            AccessoryDashSource.Veil => Player.GetModPlayer<CompositeAccessoryPlayer>().VeilEquipped,
            AccessoryDashSource.TerraWings => Player.GetModPlayer<TerraWingsPlayer>().Equipped,
            _ => false
        };

        internal void GrantContactImmunity(int ticks)
        {
            ContactImmunityTicks = Math.Max(ContactImmunityTicks, ticks);
            // 命中这一 tick 不递减，后续每游戏 tick 最多递减一次。
            lastImmunityTick = Main.GameUpdateCount;
        }

        public override void PreUpdate()
        {
            if (lastImmunityTick == Main.GameUpdateCount) return;
            lastImmunityTick = Main.GameUpdateCount;
            if (ContactImmunityTicks > 0) ContactImmunityTicks--;
        }

        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
        {
            // 接触检测早于 PreUpdateMovement；主动免疫直接核对实际效果与当前装备。
            return !(ContactImmunityTicks > 0 ||
                Player.GetModPlayer<DashPlayer>().ActiveEffect is AccessoryDash effect && effect.CanUseDash(Player));
        }

        public override void PostUpdate()
        {
            if (Player.GetModPlayer<DashPlayer>().ActiveEffect is AccessoryDash effect)
                effect.AfterMovement(Player);
        }

        internal void ResetSession()
        {
            DashPlayer dash = Player.GetModPlayer<DashPlayer>();
            if (dash.ActiveEffect is AccessoryDash) dash.EndDash();
            Selected = Configuration = null;
            Started = MovementPending = EndPending = false;
            Speed = 0f;
            ContactImmunityTicks = 0;
            lastImmunityTick = ulong.MaxValue;
        }

        public override void UpdateDead() => ResetSession();
        public override void OnEnterWorld() => ResetSession();
    }
}
