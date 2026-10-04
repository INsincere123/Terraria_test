using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public partial class EnergyShieldPlayer
    {
        internal const byte ShieldStatePacket = 0;
        private const int SyncIntervalFrames = 6;
        private ShieldSnapshot _lastSentSnapshot;
        private bool _hasSentSnapshot;
        private ulong _lastSentFrame;

        // 余额中包含卸装后保留的普通量；Maximum=0 时该余额不参与战斗或显示。
        private readonly record struct ShieldSnapshot(float Normal, float Maximum, float Temporary,
            float TemporaryInitial, ushort TemporaryFrames, ushort TemporaryDuration, ushort HitTimer,
            ushort Cooldown, byte Flags, byte Equipment, byte Flash, uint Color, uint EdgeColor)
        {
            internal void Write(BinaryWriter writer)
            {
                writer.Write(Normal);
                writer.Write(Maximum);
                writer.Write(Temporary);
                writer.Write(TemporaryInitial);
                writer.Write(TemporaryFrames);
                writer.Write(TemporaryDuration);
                writer.Write(HitTimer);
                writer.Write(Cooldown);
                writer.Write(Flags);
                writer.Write(Equipment);
                writer.Write(Flash);
                writer.Write(Color);
                writer.Write(EdgeColor);
            }

            internal static ShieldSnapshot Read(BinaryReader reader) => new(reader.ReadSingle(),
                reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadUInt16(),
                reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadByte(),
                reader.ReadByte(), reader.ReadByte(), reader.ReadUInt32(), reader.ReadUInt32());

            internal bool IsValid()
            {
                if (!float.IsFinite(Normal) || !float.IsFinite(Maximum) || !float.IsFinite(Temporary) ||
                    !float.IsFinite(TemporaryInitial) || !float.IsFinite(Maximum + TemporaryInitial) ||
                    Normal < 0f || Maximum < 0f || Temporary < 0f || TemporaryInitial < 0f ||
                    (Maximum > 0f && Normal > Maximum) || HitTimer > RechargeDelayFrames ||
                    Cooldown > SuperEnergyShieldPlayer.CooldownFrames || Flags > 15 || Equipment > 3 ||
                    Flash > ShieldHitFlashFrames)
                    return false;
                if ((Maximum > 0f && (Equipment & 1) == 0) ||
                    ((Equipment & 2) != 0 && (Equipment & 1) == 0) ||
                    ((Flags & 4) != 0 && ((Flags & 2) == 0 || Maximum <= 0f ||
                        Normal >= Maximum || HitTimer < RechargeDelayFrames)))
                    return false;
                if (TemporaryDuration == 0)
                    return TemporaryFrames == 0 && Temporary == 0f && TemporaryInitial == 0f;
                return TemporaryDuration == SuperEnergyShieldPlayer.EmergencyFrames &&
                    TemporaryFrames > 0 && TemporaryFrames <= TemporaryDuration && (Equipment & 2) != 0 &&
                    Maximum > 0f && Temporary <= TemporaryInitial * (TemporaryFrames / (float)TemporaryDuration) +
                    Math.Max(0.001f, TemporaryInitial * 0.000001f);
            }
        }

        private ShieldSnapshot CaptureSnapshot() => new(_state.NormalShield, _state.NormalMax,
            _state.TemporaryShield, _state.TemporaryInitial, (ushort)_state.TemporaryFrames,
            (ushort)_state.TemporaryDuration, (ushort)_state.HitTimer,
            (ushort)Player.GetModPlayer<SuperEnergyShieldPlayer>().Cooldown,
            (byte)((_state.Initialized ? 1 : 0) | (_state.IsFragile ? 2 : 0) |
                (_state.IsRecovering ? 4 : 0) | (Player.dead ? 8 : 0)),
            _equipmentMask, (byte)_shieldHitFlashTimer, ShieldColor.PackedValue, ShieldEdgeColor.PackedValue);

        internal void SendShieldState(bool force = false)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient || !IsAuthority) return;
            ShieldSnapshot snapshot = CaptureSnapshot();
            if (!force && _hasSentSnapshot && (snapshot == _lastSentSnapshot ||
                Main.GameUpdateCount - _lastSentFrame < SyncIntervalFrames)) return;

            WriteSnapshot(snapshot, -1, -1);
            _lastSentSnapshot = snapshot;
            _hasSentSnapshot = true;
            _lastSentFrame = Main.GameUpdateCount;
        }

        private void WriteSnapshot(ShieldSnapshot snapshot, int toWho, int ignoreClient)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write(ShieldStatePacket);
            packet.Write((byte)Player.whoAmI);
            snapshot.Write(packet);
            packet.Send(toWho, ignoreClient);
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            // 服务端持有最近一次所属客户端快照，供后来加入的客户端初始化。
            if (Main.netMode == NetmodeID.Server || IsAuthority)
                WriteSnapshot(CaptureSnapshot(), toWho, fromWho);
        }

        internal static void ReceiveShieldState(BinaryReader reader, int sender)
        {
            int index = reader.ReadByte();
            ShieldSnapshot snapshot = ShieldSnapshot.Read(reader);
            if (index >= Main.maxPlayers || !snapshot.IsValid() ||
                (Main.netMode == NetmodeID.Server && index != sender) ||
                (Main.netMode == NetmodeID.MultiplayerClient && index == Main.myPlayer)) return;

            EnergyShieldPlayer shield = Main.player[index].GetModPlayer<EnergyShieldPlayer>();
            shield._state.Restore(snapshot.Normal, snapshot.Maximum, snapshot.Temporary, snapshot.TemporaryInitial,
                snapshot.TemporaryFrames, snapshot.TemporaryDuration, snapshot.HitTimer,
                (snapshot.Flags & 1) != 0, (snapshot.Flags & 2) != 0, (snapshot.Flags & 4) != 0);
            shield._equipmentMask = snapshot.Equipment;
            shield._shieldHitFlashTimer = snapshot.Flash;
            shield.ShieldColor = new Color { PackedValue = snapshot.Color };
            shield.ShieldEdgeColor = new Color { PackedValue = snapshot.EdgeColor };
            Main.player[index].GetModPlayer<SuperEnergyShieldPlayer>().ApplySyncedCooldown(snapshot.Cooldown);
            if ((snapshot.Flags & 8) != 0)
                shield.DisplayShield = shield._filterStrength = 0f;

            if (Main.netMode == NetmodeID.Server)
                shield.WriteSnapshot(snapshot, -1, sender);
        }
    }
}
