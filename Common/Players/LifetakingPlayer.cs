using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.GlobalNPCs;
using TestMod.Common.Mechanics.Lifetaking;

namespace TestMod.Common.Players
{
    public sealed partial class LifetakingPlayer : ModPlayer
    {
        internal const int CooldownDuration = 30 * 60;
        internal const int MarkDuration = 10 * 60;
        public int Cooldown { get; private set; }
        internal int MarkTime { get; private set; }
        internal uint MarkSerial { get; private set; }
        internal int PulseTime { get; private set; }
        private int targetSlot = -1, targetType, authorizedSpear = -1;
        private uint targetGeneration;
        private ulong lastTimerTick = ulong.MaxValue;
        private ulong lastPulseSentTick = ulong.MaxValue;
        internal readonly LifetakingLedger MeleeLedger = new();

        public override void SaveData(TagCompound tag)
        {
            if (Cooldown > 0) tag["LifetakingCooldown"] = Cooldown;
        }

        public override void LoadData(TagCompound tag)
            => Cooldown = Math.Clamp(tag.GetInt("LifetakingCooldown"), 0, CooldownDuration);

        public override void OnEnterWorld()
        {
            ClearMark(false);
            authorizedSpear = -1;
            MeleeLedger.Clear();
            lastTimerTick = ulong.MaxValue;
            lastPulseSentTick = ulong.MaxValue;
            if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
            {
                var packet = RequestPacket(0);
                packet.Write((ushort)Cooldown);
                packet.Send();
            }
        }

        public override void PreUpdate() => TickTimers();

        public override void PostUpdate()
        {
            if (Player.itemAnimation == 0 && MeleeLedger.HasAttempts)
            {
                if (Player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server)
                    Systems.LifetakingAttackSystem.PlayFinish(Player.Center);
                MeleeLedger.Clear();
            }
        }

        public override void UpdateDead()
        {
            TickTimers();
            ClearMark(Main.netMode != NetmodeID.MultiplayerClient);
            authorizedSpear = -1;
            MeleeLedger.Clear();
        }

        private void TickTimers()
        {
            if (lastTimerTick == Main.GameUpdateCount) return;
            lastTimerTick = Main.GameUpdateCount;
            if (Cooldown > 0) Cooldown--;
            if (PulseTime > 0) PulseTime--;
            if (MarkTime > 0 && (--MarkTime == 0 || Main.netMode != NetmodeID.MultiplayerClient && !TryGetTarget(out _)))
                ClearMark(Main.netMode != NetmodeID.MultiplayerClient);
        }

        internal bool TryGetTarget(out NPC target)
        {
            target = null;
            if (MarkTime <= 0 || targetSlot < 0 || targetSlot >= Main.maxNPCs) return false;
            NPC npc = Main.npc[targetSlot];
            if (!npc.active || npc.life <= 0 || npc.type != targetType || targetGeneration == 0
                || npc.GetGlobalNPC<HookTargetIdentity>().Generation != targetGeneration) return false;
            target = npc;
            return true;
        }

        private void SetMark(NPC target)
        {
            targetSlot = target.whoAmI; targetType = target.type;
            targetGeneration = target.GetGlobalNPC<HookTargetIdentity>().Generation;
            MarkTime = MarkDuration;
            MarkSerial++;
            PulseTime = 12;
        }

        internal void ClearMark(bool sync)
        {
            bool changed = targetSlot >= 0;
            if (changed && TryGetTarget(out NPC oldTarget)) Systems.LifetakingAttackSystem.PlayFinish(oldTarget.Center);
            targetSlot = -1; targetGeneration = 0; MarkTime = PulseTime = 0;
            if (changed && sync) SendState();
        }

        internal void BeginSwing() => MeleeLedger.Clear();

        internal void Pulse()
        {
            PulseTime = 10;
            if (Main.netMode == NetmodeID.MultiplayerClient
                && (lastPulseSentTick == ulong.MaxValue || Main.GameUpdateCount - lastPulseSentTick >= 4))
            {
                lastPulseSentTick = Main.GameUpdateCount;
                var packet = RequestPacket(3);
                packet.Write(MarkSerial);
                packet.Send();
            }
        }
    }
}
