using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.Dashes;

namespace TestMod.Common.Players
{
    public sealed partial class CompositeAccessoryPlayer
    {
        internal const byte DashVisualPacket = 11;
        private bool remoteDashVisual;
        private bool HasDashVisual => (ShieldEquipped || VeilEquipped) && Player.active && !Player.dead
            && !Player.mount.Active && !Player.CCed && !Player.shimmering && !Player.tongued
            && !Player.pulley && Player.grapCount == 0
            && (Player.GetModPlayer<AccessoryDashPlayer>().IsSourceActive(AccessoryDashSource.Cthulhu)
                || Player.GetModPlayer<AccessoryDashPlayer>().IsSourceActive(AccessoryDashSource.Veil)
                || Main.netMode != NetmodeID.SinglePlayer && Player.whoAmI != Main.myPlayer && remoteDashVisual);

        public override void CopyClientState(ModPlayer targetCopy) =>
            ((CompositeAccessoryPlayer)targetCopy).remoteDashVisual = HasDashVisual;
        public override void SendClientChanges(ModPlayer clientPlayer)
        {
            if (((CompositeAccessoryPlayer)clientPlayer).remoteDashVisual != HasDashVisual) SendDashVisual();
        }
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => SendDashVisual(toWho, fromWho);

        private void SendDashVisual(int toWho = -1, int ignore = -1, bool? active = null)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write(DashVisualPacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write(active ?? HasDashVisual);
            packet.Send(toWho, ignore);
        }

        internal static void ReceiveDashVisual(BinaryReader reader, int sender)
        {
            int index = reader.ReadByte();
            bool active = reader.ReadBoolean();
            if (index >= Main.maxPlayers || Main.netMode == NetmodeID.Server && sender != index) return;
            Player player = Main.player[index];
            if (!player.active || !player.TryGetModPlayer(out CompositeAccessoryPlayer state)) return;
            // 仅提供远端绘制状态；本地免疫只检查实际 ActiveEffect，不信任此包。
            state.remoteDashVisual = active;
            if (Main.netMode == NetmodeID.Server) state.SendDashVisual(ignore: sender, active: active);
        }
    }
}
