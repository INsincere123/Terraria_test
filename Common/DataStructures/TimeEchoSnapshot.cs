using System.IO;
using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.Common.DataStructures
{
    internal readonly record struct TimeEchoSnapshot(Vector2 Position, int Life, int Mana,
        Rectangle BodyFrame, Rectangle LegFrame, int Direction, float Gravity, int WingFrame,
        float Rotation, Vector2 RotationOrigin)
    {
        internal static TimeEchoSnapshot Capture(Player player) => new(player.position, player.statLife,
            player.statMana, player.bodyFrame, player.legFrame, player.direction, player.gravDir,
            player.wingFrame, player.fullRotation, player.fullRotationOrigin);

        // 只发送远端绘制需要的字段；历史生命、魔力留在服务器，不由施法请求提交。
        internal void WriteVisual(BinaryWriter writer)
        {
            writer.Write(Position.X); writer.Write(Position.Y);
            WriteFrame(writer, BodyFrame); WriteFrame(writer, LegFrame);
            writer.Write((sbyte)Direction); writer.Write(Gravity);
            writer.Write(WingFrame); writer.Write(Rotation);
            writer.Write(RotationOrigin.X); writer.Write(RotationOrigin.Y);
        }

        internal static TimeEchoSnapshot ReadVisual(BinaryReader reader) => new(
            new Vector2(reader.ReadSingle(), reader.ReadSingle()), 0, 0,
            ReadFrame(reader), ReadFrame(reader), reader.ReadSByte(), reader.ReadSingle(),
            reader.ReadInt32(), reader.ReadSingle(), new Vector2(reader.ReadSingle(), reader.ReadSingle()));

        private static void WriteFrame(BinaryWriter writer, Rectangle frame)
        {
            writer.Write(frame.X); writer.Write(frame.Y); writer.Write(frame.Width); writer.Write(frame.Height);
        }
        private static Rectangle ReadFrame(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadInt32(),
            reader.ReadInt32(), reader.ReadInt32());
    }
}
