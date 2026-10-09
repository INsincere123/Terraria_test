using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public partial class TerraArmorPlayer
    {
        internal const byte StatePacket = 8;
        private bool _receivedInitialState;

        public override void CopyClientState(ModPlayer targetCopy) => ((TerraArmorPlayer)targetCopy).Mode = Mode;

        public override void SendClientChanges(ModPlayer clientPlayer)
        {
            if (((TerraArmorPlayer)clientPlayer).Mode != Mode) SendState();
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => SendState(toWho, fromWho);

        private void SendState(int toWho = -1, int ignore = -1)
        {
            if (Main.netMode == NetmodeID.Server && !_receivedInitialState) return;
            ModPacket packet = Mod.GetPacket();
            packet.Write(StatePacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write((byte)Mode);
            packet.Write((ushort)ReviveCooldown);
            packet.Send(toWho, ignore);
        }

        internal static void ReceiveState(BinaryReader reader, int sender)
        {
            int index = reader.ReadByte();
            byte mode = reader.ReadByte();
            int cooldown = reader.ReadUInt16();
            if (index >= Main.maxPlayers || mode > 3 || cooldown > ReviveCooldownTicks) return;
            Player player = Main.player[index];
            if (!player.active || !player.TryGetModPlayer(out TerraArmorPlayer armor)) return;
            if (Main.netMode == NetmodeID.Server)
            {
                if (sender != index) return;
                // 初次接入沿用客户端角色存档；后续不允许缩短复活冷却。
                if (!armor._receivedInitialState)
                {
                    armor._receivedInitialState = true;
                    armor.ReviveCooldown = cooldown;
                    armor.SetMode((TerraArmorMode)mode);
                }
                else
                {
                    armor.ReviveCooldown = Math.Max(armor.ReviveCooldown, cooldown);
                    if (!Equipped(player))
                    {
                        armor.SendState(sender);
                        return;
                    }
                    armor.SetMode((TerraArmorMode)mode);
                }
                // 本地已经应用自己的输入；不把旧确认回送给发送者覆盖更新的模式。
                armor.SendState(ignore: sender);
            }
            else if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                armor.SetMode((TerraArmorMode)mode);
                armor.ReviveCooldown = cooldown;
            }
        }
    }
}
