using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public sealed partial class MomentumConverterPlayer
    {
        internal const byte ConversionEventPacket = 3;

        private void SendConversionEvent(Vector2 center, Vector2 before, Vector2 after, byte charges, int ignoreClient = -1)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                return;
            ModPacket packet = Mod.GetPacket();
            packet.Write(ConversionEventPacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write(charges);
            packet.Write(center.X);
            packet.Write(center.Y);
            packet.Write(before.X);
            packet.Write(before.Y);
            packet.Write(after.X);
            packet.Write(after.Y);
            packet.Send(ignoreClient: ignoreClient);
        }

        internal static void ReceiveConversionEvent(BinaryReader reader, int sender)
        {
            int index = reader.ReadByte();
            byte charges = reader.ReadByte();
            Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
            Vector2 before = new(reader.ReadSingle(), reader.ReadSingle());
            Vector2 after = new(reader.ReadSingle(), reader.ReadSingle());
            if (index >= Main.maxPlayers ||
                (Main.netMode == NetmodeID.Server && sender != index) ||
                (Main.netMode == NetmodeID.MultiplayerClient && index == Main.myPlayer) ||
                !ValidEvent(center, before, after, charges))
                return;

            Player player = Main.player[index];
            if (!player.active || player.dead)
                return;
            MomentumConverterPlayer state = player.GetModPlayer<MomentumConverterPlayer>();
            if (Main.netMode == NetmodeID.Server)
            {
                if (!state.Equipped || (player.mount.Active && player.mount.Cart) || player.grapCount > 0 || player.grappling[0] >= 0)
                    return;
                // 事件只转发视觉，不根据客户端事件改动服务器或其他玩家的速度、充能。
                state.SendConversionEvent(center, before, after, charges, sender);
            }
            else
                state.PlayConversionVisuals(center, before, after);
        }

        private static bool ValidEvent(Vector2 center, Vector2 before, Vector2 after, byte charges)
        {
            if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || center.X < 0f || center.Y < 0f ||
                center.X > Main.maxTilesX * 16f || center.Y > Main.maxTilesY * 16f ||
                !TryCalculateVelocity(before, after, charges, out Vector2 expected))
                return false;
            float tolerance = Math.Max(0.001f, expected.Length() * 0.0001f);
            return Vector2.DistanceSquared(expected, after) <= tolerance * tolerance;
        }
    }
}
