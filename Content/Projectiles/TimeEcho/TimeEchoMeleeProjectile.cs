using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;

namespace TestMod.Content.Projectiles.TimeEcho
{
    public sealed class TimeEchoMeleeProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_0";
        internal TimeEchoMeleeFrame Frame { get; private set; }
        internal bool HasFrame { get; private set; }
        internal uint Swing => Frame.Swing;
        internal float PoseRotation => rotation;
        private Vector2 hitCenter, halfSize;
        private float rotation;
        private ulong lastFrameTick;

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 一次挥砍对同一目标只结算一次。
        }
        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => HasFrame ? null : false;

        internal void SetFrame(Vector2 origin, in TimeEchoMeleeFrame frame)
        {
            bool first = !HasFrame;
            Frame = frame;
            HasFrame = true;
            lastFrameTick = Main.GameUpdateCount;
            Vector2 fallback = new(frame.Direction, 0);
            rotation = (frame.Mouse - origin).SafeNormalize(fallback).ToRotation() - fallback.ToRotation();
            hitCenter = origin + (new Vector2(frame.Hitbox.Center.X, frame.Hitbox.Center.Y) - frame.PlayerCenter).RotatedBy(rotation);
            halfSize = new(frame.Hitbox.Width * 0.5f, frame.Hitbox.Height * 0.5f);
            Projectile.Center = origin;
            Projectile.timeLeft = 6;
            if (first || Main.GameUpdateCount % 3 == 0) Projectile.netUpdate = true;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.TryGetModPlayer(out TimeEchoPlayer echo) || !echo.HasPhantom ||
                (echo.attackState.ActiveTicks == 0 && (!HasFrame || Frame.Tick != Main.GameUpdateCount)) ||
                (HasFrame && Main.GameUpdateCount - lastFrameTick > 6))
                Projectile.active = false;
        }
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => HasFrame && TimeEchoAttackGeometry.Intersects(hitCenter, halfSize, rotation, targetHitbox);

        public override bool PreDraw(ref Color lightColor)
            => false; // 武器与幻影身体一起绘制，使用同一相机边界，避免原版弹幕剔除提前裁掉。

        internal void DrawWeapon(Vector2 origin, float fade)
        {
            if (!HasFrame || Main.dedServ) return;
            Main.instance.LoadItem(Frame.ItemType);
            Texture2D texture = TextureAssets.Item[Frame.ItemType].Value;
            Rectangle source = Main.itemAnimations[Frame.ItemType]?.GetFrame(texture) ?? texture.Bounds;
            SpriteEffects effects = Frame.Direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            if (Frame.Gravity < 0) effects ^= SpriteEffects.FlipVertically;
            Vector2 grip = new(Frame.Direction > 0 ? 0 : source.Width, Frame.Gravity > 0 ? source.Height : 0);
            Main.EntitySpriteDraw(texture, origin + Frame.GripOffset.RotatedBy(rotation) - Main.screenPosition, source,
                Color.Lerp(TimeEchoSystem.EchoBlue, TimeEchoSystem.EchoPurple, 0.4f) * (0.65f * fade),
                Frame.Rotation + rotation, grip, Frame.Scale, effects);
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(HasFrame);
            if (!HasFrame) return;
            writer.Write(hitCenter.X); writer.Write(hitCenter.Y);
            writer.Write(halfSize.X); writer.Write(halfSize.Y); writer.Write(rotation);
            writer.Write(Frame.ItemType); writer.Write(Frame.Scale); writer.Write(Frame.Rotation);
            writer.Write(Frame.Direction); writer.Write(Frame.Gravity); writer.Write(Frame.Swing);
            writer.Write(Frame.GripOffset.X); writer.Write(Frame.GripOffset.Y);
            writer.Write(Frame.BodyFrame.X); writer.Write(Frame.BodyFrame.Y);
            writer.Write(Frame.BodyFrame.Width); writer.Write(Frame.BodyFrame.Height);
            WriteArm(writer, Frame.FrontArm); WriteArm(writer, Frame.BackArm);
        }
        public override void ReceiveExtraAI(BinaryReader reader)
        {
            HasFrame = reader.ReadBoolean();
            if (!HasFrame) return;
            hitCenter = new(reader.ReadSingle(), reader.ReadSingle());
            halfSize = new(reader.ReadSingle(), reader.ReadSingle()); rotation = reader.ReadSingle();
            int item = reader.ReadInt32(); float scale = reader.ReadSingle(), itemRotation = reader.ReadSingle();
            int direction = reader.ReadInt32(); float gravity = reader.ReadSingle(); uint swing = reader.ReadUInt32();
            Vector2 grip = new(reader.ReadSingle(), reader.ReadSingle());
            Rectangle body = new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            Player.CompositeArmData front = ReadArm(reader), back = ReadArm(reader);
            if (item <= 0 || item >= ItemLoader.ItemCount || !float.IsFinite(rotation) ||
                !float.IsFinite(hitCenter.X) || !float.IsFinite(hitCenter.Y) ||
                !float.IsFinite(halfSize.X) || !float.IsFinite(halfSize.Y) || halfSize.X < 0 || halfSize.Y < 0)
            { HasFrame = false; return; }
            Frame = new(default, default, default, Projectile.damage, Projectile.CritChance, Projectile.knockBack,
                Projectile.ArmorPenetration, Projectile.DamageType, item, grip, scale, itemRotation, direction, gravity,
                body, front, back, swing, Main.GameUpdateCount);
            lastFrameTick = Main.GameUpdateCount;
        }
        private static void WriteArm(BinaryWriter writer, Player.CompositeArmData arm)
        { writer.Write(arm.enabled); writer.Write((byte)arm.stretch); writer.Write(arm.rotation); }
        private static Player.CompositeArmData ReadArm(BinaryReader reader)
            => new(reader.ReadBoolean(), (Player.CompositeArmStretchAmount)reader.ReadByte(), reader.ReadSingle());
    }
}
