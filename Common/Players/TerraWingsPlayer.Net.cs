using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.Dashes;

namespace TestMod.Common.Players
{
    public partial class TerraWingsPlayer
    {
        internal const byte DashVisualPacket = 10;
        private bool remoteDashVisual;
        private bool HasDashVisual => Equipped && Player.active && !Player.dead && !Player.mount.Active
            && !Player.CCed && !Player.shimmering && !Player.tongued && !Player.pulley && Player.grapCount == 0
            && (Player.GetModPlayer<DashPlayer>().ActiveEffect is TerraWingsDash
                || Main.netMode != NetmodeID.SinglePlayer && Player.whoAmI != Main.myPlayer && remoteDashVisual);

        public override void CopyClientState(ModPlayer targetCopy)
            => ((TerraWingsPlayer)targetCopy).remoteDashVisual = HasDashVisual;

        public override void SendClientChanges(ModPlayer clientPlayer)
        {
            if (((TerraWingsPlayer)clientPlayer).remoteDashVisual != HasDashVisual) SendDashVisual();
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => SendDashVisual(toWho, fromWho);

        private void SendDashVisual(int toWho = -1, int ignore = -1)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write(DashVisualPacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write(HasDashVisual);
            packet.Send(toWho, ignore);
        }

        internal static void ReceiveDashVisual(BinaryReader reader, int sender)
        {
            int index = reader.ReadByte();
            bool active = reader.ReadBoolean();
            if (index >= Main.maxPlayers || Main.netMode == NetmodeID.Server && sender != index) return;
            Player player = Main.player[index];
            if (!player.active || !player.TryGetModPlayer(out TerraWingsPlayer state)) return;
            // 此包仅同步绘制状态，不能发动冲刺、授予免疫或投送伤害。
            state.remoteDashVisual = active;
            if (Main.netMode == NetmodeID.Server) state.SendDashVisual(ignore: sender);
        }
    }
}
