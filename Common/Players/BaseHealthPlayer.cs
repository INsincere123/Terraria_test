using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public sealed class BaseHealthPlayer : ModPlayer
    {
        internal const byte StatePacket = 24;
        internal const int MaximumBaseHealth = short.MaxValue;
        // 0 表示正常计算；仅会话状态，不保存、不修改永久成长计数。
        internal int BaseHealth { get; private set; }

        public override void Initialize() => Clear();
        public override void OnEnterWorld() => Clear();
        public override void PlayerDisconnect() => Clear();
        internal void Clear() => BaseHealth = 0;

        internal void SetBaseHealth(int value)
        {
            BaseHealth = value;
            // 不重跑装备/Buff，也不主动治疗；下次正常属性更新应用新上限。
            if (Main.netMode == NetmodeID.MultiplayerClient) SendState();
        }

        internal static void ApplyBaseHealth(Player player)
        {
            if (player.TryGetModPlayer(out BaseHealthPlayer state) && state.BaseHealth > 0)
                player.statLifeMax = state.BaseHealth;
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => SendState(toWho, fromWho);

        private void SendState(int toWho = -1, int ignore = -1)
        {
            if (Main.netMode == NetmodeID.SinglePlayer) return;
            var packet = Mod.GetPacket();
            packet.Write(StatePacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write((ushort)BaseHealth);
            packet.Send(toWho, ignore);
        }

        internal static void ReceiveState(BinaryReader reader, int sender)
        {
            int index = reader.ReadByte();
            int value = reader.ReadUInt16();
            if (index >= Main.maxPlayers || value > MaximumBaseHealth ||
                Main.netMode == NetmodeID.Server && index != sender ||
                Main.netMode == NetmodeID.MultiplayerClient && index == Main.myPlayer) return;
            Player player = Main.player[index];
            if (!player.active || !player.TryGetModPlayer(out BaseHealthPlayer state)) return;
            state.BaseHealth = value;
            if (Main.netMode == NetmodeID.Server) state.SendState(ignore: sender);
        }
    }
}
