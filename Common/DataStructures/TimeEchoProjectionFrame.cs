using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using TestMod.Common.Utilities;

namespace TestMod.Common.DataStructures
{
    internal readonly record struct TimeEchoLineSegment(Vector2 Start, Vector2 End, float Width);
    // 只同步可回放的几何和姿态，不发送第三方私有状态或本机弹幕槽位。
    internal readonly record struct TimeEchoProjectionFrame(bool Damaging, bool HasRectangle,
        Vector2 HitCenter, Vector2 HalfSize, float HitRotation, bool HasLine,
        Vector2 LineStart, Vector2 LineEnd, float LineWidth, Vector2 SpriteCenter,
        float SpriteRotation, float Scale, int SpriteFrame, int SpriteFrameCount, int SpriteDirection,
        Vector2 SpriteOrigin, bool DrawSprite, Vector2 OwnerCenter, bool OwnerHitCheck,
        int SourceType, int StaticCooldown, bool HasCircle = false, Vector2 CircleCenter = default, float CircleRadius = 0,
        TimeEchoLineSegment[] Segments = null)
    {
        internal const int MaxSegments = 64;
        internal bool IsValid => SourceType > 0 && SourceType < Terraria.ModLoader.ProjectileLoader.ProjectileCount &&
            Finite(HitCenter) && Finite(HalfSize) && Finite(LineStart) && Finite(LineEnd) &&
            Finite(SpriteCenter) && Finite(SpriteOrigin) && Finite(OwnerCenter) && Finite(CircleCenter) &&
            float.IsFinite(HitRotation) && float.IsFinite(SpriteRotation) &&
            float.IsFinite(Scale) && Scale >= 0 && Scale <= 100 &&
            float.IsFinite(LineWidth) && LineWidth >= 0 && LineWidth <= 100000 &&
            HalfSize.X >= 0 && HalfSize.Y >= 0 && HalfSize.X <= 100000 && HalfSize.Y <= 100000 &&
            Vector2.DistanceSquared(LineStart, LineEnd) <= 100000f * 100000f &&
            // 纯几何光束不读取贴图；第三方可以把 frame 当作递增计时器。
            (!DrawSprite || (SpriteFrameCount > 0 && SpriteFrameCount <= 4096 && SpriteFrame >= 0 && SpriteFrame < SpriteFrameCount)) &&
            StaticCooldown >= 0 && StaticCooldown <= 36000 &&
            float.IsFinite(CircleRadius) && CircleRadius >= 0 && CircleRadius <= 100000 && ValidSegments();

        private bool ValidSegments()
        {
            if (Segments == null) return true;
            if (Segments.Length > MaxSegments) return false;
            foreach (TimeEchoLineSegment segment in Segments)
                if (!Finite(segment.Start) || !Finite(segment.End) || !float.IsFinite(segment.Width) ||
                    segment.Width < 0 || segment.Width > 100000 ||
                    Vector2.DistanceSquared(segment.Start, segment.End) > 100000f * 100000f) return false;
            return true;
        }

        private static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);

        internal TimeEchoProjectionFrame Transform(Vector2 sourceOrigin, Vector2 origin, float rotation)
        {
            Vector2 Move(Vector2 p) => origin + (p - sourceOrigin).RotatedBy(rotation);
            TimeEchoLineSegment[] moved = null;
            if (Segments != null)
            {
                moved = new TimeEchoLineSegment[Segments.Length];
                for (int i = 0; i < moved.Length; i++)
                    moved[i] = new(Move(Segments[i].Start), Move(Segments[i].End), Segments[i].Width);
            }
            return this with { HitCenter = Move(HitCenter), HitRotation = HitRotation + rotation,
                LineStart = Move(LineStart), LineEnd = Move(LineEnd), SpriteCenter = Move(SpriteCenter),
                SpriteRotation = SpriteRotation + rotation, OwnerCenter = Move(OwnerCenter), CircleCenter = Move(CircleCenter), Segments = moved };
        }

        internal bool Intersects(Rectangle target)
        {
            if (HasRectangle && TimeEchoAttackGeometry.Intersects(HitCenter, HalfSize, HitRotation, target)) return true;
            if (HasCircle && TimeEchoAttackGeometry.IntersectsCircle(CircleCenter, CircleRadius, target)) return true;
            if (Segments != null)
                foreach (TimeEchoLineSegment segment in Segments)
                {
                    float collisionPoint = 0;
                    if (segment.Width == 0
                        ? Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), segment.Start, segment.End)
                        : Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), segment.Start, segment.End, segment.Width, ref collisionPoint)) return true;
                }
            if (!HasLine) return false;
            if (LineWidth == 0) return Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), LineStart, LineEnd);
            float point = 0;
            return Collision.CheckAABBvLineCollision(target.TopLeft(), target.Size(), LineStart, LineEnd, LineWidth, ref point);
        }

        internal void Write(BinaryWriter writer)
        {
            writer.Write(Damaging); writer.Write(HasRectangle);
            WriteVector(writer, HitCenter); WriteVector(writer, HalfSize); writer.Write(HitRotation);
            writer.Write(HasLine); WriteVector(writer, LineStart); WriteVector(writer, LineEnd); writer.Write(LineWidth);
            WriteVector(writer, SpriteCenter); writer.Write(SpriteRotation); writer.Write(Scale);
            writer.Write(SpriteFrame); writer.Write(SpriteFrameCount); writer.Write(SpriteDirection); WriteVector(writer, SpriteOrigin);
            writer.Write(DrawSprite); WriteVector(writer, OwnerCenter); writer.Write(OwnerHitCheck);
            writer.Write(SourceType); writer.Write(StaticCooldown);
            writer.Write(HasCircle); WriteVector(writer, CircleCenter); writer.Write(CircleRadius);
            writer.Write((byte)(Segments?.Length ?? 0));
            if (Segments != null)
                foreach (TimeEchoLineSegment segment in Segments)
                { WriteVector(writer, segment.Start); WriteVector(writer, segment.End); writer.Write(segment.Width); }
        }

        internal static TimeEchoProjectionFrame Read(BinaryReader reader)
        {
            var frame = new TimeEchoProjectionFrame(reader.ReadBoolean(), reader.ReadBoolean(),
            ReadVector(reader), ReadVector(reader), reader.ReadSingle(), reader.ReadBoolean(),
            ReadVector(reader), ReadVector(reader), reader.ReadSingle(), ReadVector(reader), reader.ReadSingle(),
            reader.ReadSingle(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), ReadVector(reader), reader.ReadBoolean(),
            ReadVector(reader), reader.ReadBoolean(), reader.ReadInt32(), reader.ReadInt32(),
            reader.ReadBoolean(), ReadVector(reader), reader.ReadSingle());
            int count = reader.ReadByte();
            if (count > MaxSegments) throw new InvalidDataException("TimeEcho segment count exceeds limit");
            if (count == 0) return frame;
            var segments = new TimeEchoLineSegment[count];
            for (int i = 0; i < count; i++) segments[i] = new(ReadVector(reader), ReadVector(reader), reader.ReadSingle());
            return frame with { Segments = segments };
        }
        private static void WriteVector(BinaryWriter writer, Vector2 p) { writer.Write(p.X); writer.Write(p.Y); }
        private static Vector2 ReadVector(BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());
    }
}
