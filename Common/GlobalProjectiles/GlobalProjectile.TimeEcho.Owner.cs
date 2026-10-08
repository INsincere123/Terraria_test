using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using TestMod.Common.Systems;
using TestMod.Common.DataStructures;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        private TimeEchoOwnerPose echoOwnerPose;
        private Vector2 echoOwnerOrigin;
        private Player echoOwnerProxy;
        private ulong echoOwnerTick;
        private Projectile echoControlSource;
        private int echoControlIdentity = -1, echoControlType;
        private bool echoControlResolved;

        private void InitializeEchoOwner(Projectile p, TimeEchoOwnerPose pose, Vector2 origin, Projectile controlSource = null)
        {
            echoOwnerOrigin = origin;
            echoOwnerTick = Main.GameUpdateCount;
            if (!TimeEchoOwnerPose.NeedsProxy(p)) return;
            echoOwnerPose = pose.Animation > 0 ? pose : TimeEchoOwnerPose.Capture(Main.player[p.owner], p);
            ApplyEchoWhipSettings(p);
            if (TimeEchoAttackSystem.IsDirectHeldAttack(p) && controlSource != null)
            {
                echoControlSource = controlSource;
                echoControlIdentity = controlSource.identity;
                echoControlType = controlSource.type;
                echoControlResolved = true;
            }
        }

        private int EchoAnimationLeft => Math.Max(0, echoOwnerPose.AnimationLeft -
            (int)Math.Min((ulong)int.MaxValue, Main.GameUpdateCount - echoOwnerTick));

        private bool IsEchoControlSourceValid(Projectile p) => echoControlSource != null &&
            echoControlSource.active && echoControlSource.identity == echoControlIdentity &&
            echoControlSource.type == echoControlType && echoControlSource.owner == p.owner &&
            ReferenceEquals(Main.projectile[echoControlSource.whoAmI], echoControlSource) &&
            !echoControlSource.GetGlobalProjectile<GlobalProjectile>().IsTimeEchoAttack;

        private bool UpdateEchoControl(Projectile p, TimeEchoPlayer echo)
        {
            if (!TimeEchoAttackSystem.IsDirectHeldAttack(p)) return true;
            if (!echo.CanCopyAttack || echoControlIdentity < 0) { p.active = false; return false; }
            if (!echoControlResolved)
            {
                // 远端槽位不同，只在首次解析时使用现有索引；原载体尚未到包时短暂等待。
                foreach (Projectile candidate in ProjectileLookup.Owned(p.owner, echoControlType))
                    if (candidate.identity == echoControlIdentity &&
                        !candidate.GetGlobalProjectile<GlobalProjectile>().IsTimeEchoAttack)
                    { echoControlSource = candidate; echoControlResolved = true; break; }
                if (!echoControlResolved)
                {
                    if (Main.GameUpdateCount - echoOwnerTick > 10) p.active = false;
                    else p.timeLeft = Math.Max(p.timeLeft, 2);
                    return false;
                }
            }
            if (!IsEchoControlSourceValid(p)) { p.active = false; return false; }
            return true;
        }

        private void ApplyEchoWhipSettings(Projectile p)
        {
            if (echoOwnerPose.WhipSegments <= 0 || echoOwnerPose.WhipBaseRange <= 0) return;
            p.WhipSettings.Segments = echoOwnerPose.WhipSegments;
            p.WhipSettings.RangeMultiplier = echoOwnerPose.WhipBaseRange;
        }

        internal Player GetEchoOwner(Projectile p, Player realOwner)
        {
            if (!IsTimeEchoAttack || !TimeEchoOwnerPose.NeedsProxy(p) || echoOwnerPose.Animation <= 0) return realOwner;
            if (echoOwnerProxy == null)
            {
                echoOwnerProxy = new Player();
                if (echoOwnerPose.ItemType > 0 && echoOwnerPose.ItemType < ItemLoader.ItemCount)
                    echoOwnerProxy.inventory[0].SetDefaults(echoOwnerPose.ItemType);
            }
            Player proxy = echoOwnerProxy;
            proxy.whoAmI = p.owner;
            proxy.width = realOwner.width; proxy.height = realOwner.height;
            Vector2 origin = echoOwnerOrigin;
            if (realOwner.TryGetModPlayer(out TimeEchoPlayer echo) && echo.HasPhantom) origin = echo.AttackOrigin;
            proxy.Center = origin;
            proxy.active = true; proxy.dead = false;
            proxy.direction = Math.Abs(p.velocity.X) > 0.001f ? Math.Sign(p.velocity.X) : echoOwnerPose.Direction;
            proxy.gravDir = echoOwnerPose.Gravity;
            proxy.whipRangeMultiplier = echoOwnerPose.WhipRange;
            proxy.bodyFrame = echoOwnerPose.BodyFrame;
            proxy.fullRotation = echoOwnerPose.Rotation; proxy.fullRotationOrigin = echoOwnerPose.RotationOrigin;
            proxy.itemAnimationMax = echoOwnerPose.Animation;
            proxy.itemAnimation = p.aiStyle == ProjAIStyleID.Spear ? EchoAnimationLeft : echoOwnerPose.Animation;
            proxy.GetAttackSpeed(DamageClass.Melee) = 1f / echoOwnerPose.InverseMeleeSpeed;
            // 持续近战沿真实载体生命周期；不把真实玩家换进 AI，也不重复执行物品使用。
            proxy.channel = TimeEchoAttackSystem.IsDirectHeldAttack(p) && IsEchoControlSourceValid(p) &&
                realOwner.channel && realOwner.HeldItem.type == echoOwnerPose.ItemType && !realOwner.noItems && !realOwner.CCed;
            proxy.selectedItem = 0;
            // 独立玩家对象只供指定原版 AI/碰撞/鞭控制点读取，写入手持与物品时间也只影响代理。
            return proxy;
        }

        private void WriteEchoOwner(BinaryWriter writer)
        {
            writer.Write(echoOwnerPose.Animation > 0);
            if (echoOwnerPose.Animation <= 0) return;
            writer.Write(echoOwnerOrigin.X); writer.Write(echoOwnerOrigin.Y);
            writer.Write(echoOwnerPose.ItemType); writer.Write(echoOwnerPose.Animation); writer.Write(echoOwnerPose.Direction);
            writer.Write(echoOwnerPose.Gravity); writer.Write(echoOwnerPose.WhipRange);
            writer.Write(echoOwnerPose.BodyFrame.X); writer.Write(echoOwnerPose.BodyFrame.Y);
            writer.Write(echoOwnerPose.BodyFrame.Width); writer.Write(echoOwnerPose.BodyFrame.Height);
            writer.Write(echoOwnerPose.Rotation); writer.Write(echoOwnerPose.RotationOrigin.X); writer.Write(echoOwnerPose.RotationOrigin.Y);
            writer.Write(echoOwnerPose.WhipSegments); writer.Write(echoOwnerPose.WhipBaseRange);
            writer.Write(EchoAnimationLeft); writer.Write(echoOwnerPose.InverseMeleeSpeed);
            writer.Write(echoControlIdentity); writer.Write(echoControlType);
        }
        private void ReadEchoOwner(Projectile p, BinaryReader reader)
        {
            if (!reader.ReadBoolean()) { echoOwnerPose = default; echoOwnerProxy = null; return; }
            echoOwnerOrigin = new(reader.ReadSingle(), reader.ReadSingle());
            int item = reader.ReadInt32(), animation = reader.ReadInt32(), direction = reader.ReadInt32();
            float gravity = reader.ReadSingle(), range = reader.ReadSingle();
            Rectangle frame = new(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            float rotation = reader.ReadSingle(); Vector2 rotationOrigin = new(reader.ReadSingle(), reader.ReadSingle());
            int segments = reader.ReadInt32(); float baseRange = reader.ReadSingle();
            int animationLeft = reader.ReadInt32(); float inverseSpeed = reader.ReadSingle();
            int controlIdentity = reader.ReadInt32(), controlType = reader.ReadInt32();
            echoOwnerTick = Main.GameUpdateCount;
            if (echoControlIdentity != controlIdentity || echoControlType != controlType)
            { echoControlSource = null; echoControlResolved = false; }
            echoControlIdentity = controlIdentity; echoControlType = controlType;
            if (item <= 0 || item >= ItemLoader.ItemCount || animation <= 0 || !float.IsFinite(range) || range <= 0 ||
                !float.IsFinite(inverseSpeed) || inverseSpeed <= 0 || !float.IsFinite(rotationOrigin.X) ||
                !float.IsFinite(rotationOrigin.Y) || !float.IsFinite(echoOwnerOrigin.X) || !float.IsFinite(echoOwnerOrigin.Y) || !float.IsFinite(rotation))
            { echoOwnerPose = default; echoOwnerProxy = null; return; }
            echoOwnerPose = new(item, animation, direction == -1 ? -1 : 1, gravity == -1 ? -1 : 1,
                range, frame, rotation, rotationOrigin, segments, baseRange, Math.Clamp(animationLeft, 0, animation), inverseSpeed);
            ApplyEchoWhipSettings(p);
            // 普通位置同步不重建对象；换武器快照时才更新代理物品。
            if (echoOwnerProxy != null && echoOwnerProxy.inventory[0].type != item) echoOwnerProxy.inventory[0].SetDefaults(item);
        }
    }
}
