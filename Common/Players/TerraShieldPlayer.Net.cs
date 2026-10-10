using System;
using System.IO;
using Terraria;
using Terraria.ID;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.Players
{
    public sealed partial class TerraShieldPlayer
    {
        internal const byte StatePacket = 9;
        private bool _hasSentState;
        private ulong _lastSentTick;
        private double _lastSentBleed, _lastSentPartial;
        private int _lastSentCooldown;

        private void WriteState(int toWho, int ignoreClient)
        {
            var packet = Mod.GetPacket();
            packet.Write(StatePacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write(_bleed.Buffer);
            packet.Write(_bleed.Partial);
            packet.Write((ushort)DodgeCooldown);
            packet.Send(toWho, ignoreClient);
        }

        private void SendState(bool force = false)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || !IsAuthority) return;
            if (!force && _hasSentState && ((_lastSentBleed == _bleed.Buffer &&
                _lastSentPartial == _bleed.Partial && _lastSentCooldown == DodgeCooldown) ||
                Main.GameUpdateCount - _lastSentTick < 6)) return;
            WriteState(-1, -1);
            _hasSentState = true;
            _lastSentTick = Main.GameUpdateCount;
            _lastSentBleed = _bleed.Buffer;
            _lastSentPartial = _bleed.Partial;
            _lastSentCooldown = DodgeCooldown;
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            if (Main.netMode == NetmodeID.Server || IsAuthority) WriteState(toWho, fromWho);
        }

        internal static void ReceiveState(BinaryReader reader, int sender)
        {
            int index = reader.ReadByte();
            double buffer = reader.ReadDouble(), partial = reader.ReadDouble();
            int cooldown = reader.ReadUInt16();
            if (index >= Main.maxPlayers || !double.IsFinite(buffer) || !double.IsFinite(partial) ||
                buffer < 0 || buffer > int.MaxValue || partial < 0 || partial >= 1 ||
                cooldown > TerraShield.ExtraDodgeCooldownTicks ||
                (Main.netMode == NetmodeID.Server && index != sender) ||
                (Main.netMode == NetmodeID.MultiplayerClient && index == Main.myPlayer)) return;
            Player player = Main.player[index];
            if (!player.active || !player.TryGetModPlayer(out TerraShieldPlayer shield)) return;
            shield._bleed.Restore(buffer, partial);
            shield.DodgeCooldown = cooldown;
            if (Main.netMode == NetmodeID.Server) shield.WriteState(-1, sender);
        }
    }
}
