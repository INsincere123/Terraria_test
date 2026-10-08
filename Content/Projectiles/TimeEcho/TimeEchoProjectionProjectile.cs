using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;
using EchoProjectile = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Content.Projectiles.TimeEcho
{
    // 通用回放实体。第三方 AI、OnHit、OnKill 和 PreDraw 永远不在这个实体上运行。
    public sealed class TimeEchoProjectionProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_0";
        private TimeEchoProjectionFrame frame;
        private bool hasFrame;
        private ulong lastFrameTick;
        private uint generation;
        private Projectile source;
        private int sourceIdentity, sourceType;
        private bool ownerRelative, clipBeam, parentRelative, worldTarget;
        private float[] laserSamples;
        private Vector2 sourceOrigin, echoOrigin;
        private float rotation;

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 8;
        }
        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => hasFrame && frame.Damaging ? null : false;

        internal bool Matches(Projectile original, uint attackGeneration) => ReferenceEquals(source, original) &&
            sourceIdentity == original.identity && sourceType == original.type && original.owner == Projectile.owner &&
            generation == attackGeneration;

        internal void Bind(Projectile original, TimeEchoPlayer echo, uint attackGeneration, bool relative, bool clip,
            bool followParent = false, bool preserveWorld = false)
        {
            source = original;
            sourceIdentity = original.identity; sourceType = original.type; generation = attackGeneration;
            ownerRelative = relative;
            clipBeam = clip;
            parentRelative = followParent; worldTarget = preserveWorld;
            if (clipBeam) laserSamples = new float[3];
            sourceOrigin = echo.Player.Center; echoOrigin = echo.AttackOrigin;
            rotation = AimRotation(echo.Player, echoOrigin);
            Projectile.extraUpdates = original.extraUpdates;
            Projectile.penetrate = original.penetrate; Projectile.maxPenetrate = original.maxPenetrate;
            Projectile.usesLocalNPCImmunity = original.usesLocalNPCImmunity || original.usesIDStaticNPCImmunity;
            Projectile.localNPCHitCooldown = original.usesIDStaticNPCImmunity
                ? Math.Max(1, original.idStaticNPCHitCooldown * original.MaxUpdates) : original.localNPCHitCooldown;
        }

        private static float AimRotation(Player player, Vector2 origin)
        {
            Vector2 fallback = new(player.direction, 0);
            return (Main.MouseWorld - origin).SafeNormalize(fallback).ToRotation() -
                (Main.MouseWorld - player.Center).SafeNormalize(fallback).ToRotation();
        }

        internal void UpdateFrame(Projectile original, TimeEchoPlayer echo, in TimeEchoProjectionFrame captured)
        {
            if (!Matches(original, echo.attackState.Generation) || !Projectile.active) return;
            if (parentRelative)
            {
                Projectile parent = original.GetGlobalProjectile<EchoProjectile>().EchoSourceParent;
                if (parent == null || !parent.GetGlobalProjectile<EchoProjectile>().TryGetEchoCounterpart(generation, out Projectile counterpart))
                { Projectile.active = false; return; }
                sourceOrigin = parent.Center; echoOrigin = counterpart.Center;
                rotation = counterpart.velocity.SafeNormalize(Vector2.UnitX).ToRotation() -
                    parent.velocity.SafeNormalize(Vector2.UnitX).ToRotation();
            }
            else if (ownerRelative && !worldTarget)
            {
                sourceOrigin = echo.Player.Center; echoOrigin = echo.AttackOrigin;
                rotation = AimRotation(echo.Player, echoOrigin);
            }
            TimeEchoProjectionFrame next = worldTarget ? captured : captured.Transform(sourceOrigin, echoOrigin, rotation);
            if (clipBeam && next.HasLine)
            {
                Vector2 line = next.LineEnd - next.LineStart;
                Collision.LaserScan(next.LineStart, line.SafeNormalize(Vector2.UnitX), next.LineWidth, line.Length(), laserSamples);
                float length = Math.Min(laserSamples[0], Math.Min(laserSamples[1], laserSamples[2]));
                next = next with { LineEnd = next.LineStart + line.SafeNormalize(Vector2.UnitX) * length };
            }
            bool changedWindow = !hasFrame || frame.Damaging != next.Damaging;
            frame = next; hasFrame = true; lastFrameTick = Main.GameUpdateCount;
            Projectile.Center = frame.SpriteCenter;
            Projectile.damage = original.damage; Projectile.originalDamage = original.originalDamage;
            Projectile.knockBack = original.knockBack; Projectile.DamageType = original.DamageType;
            Projectile.CritChance = original.CritChance; Projectile.ArmorPenetration = original.ArmorPenetration;
            Projectile.timeLeft = Math.Max(8, Projectile.MaxUpdates * 8);
            if (changedWindow || Main.GameUpdateCount % 3 == 0) Projectile.netUpdate = true;
        }

        public override void AI()
        {
            if (!Main.player[Projectile.owner].TryGetModPlayer(out TimeEchoPlayer echo) || !echo.CanCopyAttack ||
                !hasFrame || Main.GameUpdateCount - lastFrameTick > 8)
            { Projectile.active = false; return; }
            if (Projectile.owner == Main.myPlayer && Main.netMode != NetmodeID.Server &&
                (source == null || !source.active || !Matches(source, echo.attackState.Generation) ||
                 !ReferenceEquals(Main.projectile[source.whoAmI], source))) Projectile.active = false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => hasFrame && frame.Intersects(targetHitbox);

        public override bool? CanHitNPC(NPC target)
        {
            if (!hasFrame || !frame.Damaging) return false;
            if (frame.OwnerHitCheck && !Collision.CanHitLine(frame.OwnerCenter, 1, 1, target.position, target.width, target.height))
                return false;
            if (frame.StaticCooldown > 0 && Main.player[Projectile.owner].TryGetModPlayer(out TimeEchoPlayer echo) &&
                echo.HasProjectionImmunity(frame.SourceType, target)) return false;
            return null;
        }
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (frame.StaticCooldown > 0 && Main.player[Projectile.owner].TryGetModPlayer(out TimeEchoPlayer echo))
                echo.SetProjectionImmunity(frame.SourceType, target, frame.StaticCooldown);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!hasFrame || Main.dedServ) return false;
            Color color = Color.Lerp(TimeEchoSystem.EchoBlue, TimeEchoSystem.EchoPurple, 0.4f) * 0.65f;
            if (frame.DrawSprite)
            {
                Main.instance.LoadProjectile(frame.SourceType);
                Texture2D texture = TextureAssets.Projectile[frame.SourceType].Value;
                int count = frame.SpriteFrameCount;
                Rectangle area = texture.Frame(1, count, 0, Math.Clamp(frame.SpriteFrame, 0, count - 1));
                Main.EntitySpriteDraw(texture, frame.SpriteCenter - Main.screenPosition, area, color,
                    frame.SpriteRotation, frame.SpriteOrigin, frame.Scale,
                    frame.SpriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
            }
            else if (frame.Segments is { Length: > 0 } segments)
            {
                foreach (TimeEchoLineSegment segment in segments)
                    DrawUtils.DrawLine(Main.spriteBatch, segment.Start, segment.End, color, Math.Max(1, segment.Width));
            }
            else if (frame.HasCircle)
            {
                // 用既有线纹理描边，不新增资源或执行原光环绘制。
                Vector2 previous = frame.CircleCenter + Vector2.UnitX * frame.CircleRadius;
                for (int i = 1; i <= 24; i++)
                {
                    Vector2 next = frame.CircleCenter + (MathHelper.TwoPi * i / 24).ToRotationVector2() * frame.CircleRadius;
                    DrawUtils.DrawLine(Main.spriteBatch, previous, next, color, 2); previous = next;
                }
            }
            else if (frame.HasLine)
                DrawUtils.DrawLine(Main.spriteBatch, frame.LineStart, frame.LineEnd, color, Math.Max(1, frame.LineWidth));
            else if (frame.HasRectangle)
            {
                // 无可见原贴图的射流用已有线纹理表示，不执行原弹幕的粒子或绘制回调。
                Vector2 offset = frame.SpriteRotation.ToRotationVector2() * frame.HalfSize.X;
                DrawUtils.DrawLine(Main.spriteBatch, frame.SpriteCenter - offset, frame.SpriteCenter + offset,
                    color, Math.Max(1, frame.HalfSize.Y));
            }
            return false;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(hasFrame); writer.Write(generation);
            if (hasFrame) frame.Write(writer);
        }
        public override void ReceiveExtraAI(BinaryReader reader)
        {
            hasFrame = reader.ReadBoolean(); generation = reader.ReadUInt32();
            if (hasFrame) { frame = TimeEchoProjectionFrame.Read(reader); hasFrame = frame.IsValid; }
            lastFrameTick = Main.GameUpdateCount;
        }
    }
}
