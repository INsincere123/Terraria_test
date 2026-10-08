using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;

namespace TestMod.Common.Systems
{
    public sealed partial class TimeEchoAttackSystem
    {
        // 发射与本体回放可以并存；模组适配负责解释形状，不改变普通弹幕的默认准入。
        internal sealed class AttackAdapter
        {
            internal bool Emitter, OwnerRelative = true, ClipBeam, Failed, RequiresParent, ParentRelative, WorldTarget;
            internal Func<Projectile, TimeEchoProjectionFrame> Capture;
            internal bool ReplaysBody => Capture != null || !Emitter;
        }
        private static readonly Dictionary<int, AttackAdapter> attackAdapters = new();
        private static readonly HashSet<int> borrowedBallisticTypes = new();
        // 独立射弹的坐标策略与载体适配分开，登记世界目标不会阻止其原生 AI 副本。
        private static readonly Dictionary<int, TimeEchoShotCoordinates> shotCoordinates = new();
        private const string CalamityBases = "CalamityMod.Projectiles.BaseProjectiles.";

        internal static AttackAdapter GetAttackAdapter(Projectile p)
            => attackAdapters.TryGetValue(p.type, out AttackAdapter adapter) ? adapter : null;

        internal static TimeEchoShotCoordinates GetShotCoordinates(Projectile p)
            => shotCoordinates.TryGetValue(p.type, out TimeEchoShotCoordinates coordinates)
                ? coordinates : TimeEchoShotCoordinates.MuzzleRelative;

        private void LoadAttackAdapters()
        {
            attackAdapters.Clear();
            borrowedBallisticTypes.Clear();
            shotCoordinates.Clear();
            worldEffectTypes.Clear();
            foreach (ModProjectile template in ModContent.GetContent<ModProjectile>())
            {
                try
                {
                    if (HasOnlyBorrowedFlightAI(template.GetType())) borrowedBallisticTypes.Add(template.Type);
                    if (template.GetType().FullName == "CalamityMod.Projectiles.Ranged.AngelicBeam")
                        shotCoordinates[template.Type] = TimeEchoShotCoordinates.WorldTarget;
                    RegisterRangedPolicies(template);
                    AttackAdapter adapter = CreateAttackAdapter(template.GetType());
                    if (adapter != null) attackAdapters[template.Type] = adapter;
                }
                catch (Exception exception)
                {
                    attackAdapters[template.Type] = new() { Failed = true };
                    Mod.Logger.Warn($"TimeEcho adapter registration failed for {template.FullName}; original attack is unchanged.", exception);
                }
            }
        }

        private static bool HasOnlyBorrowedFlightAI(Type type)
            // 没有自定义 AI 的普通箭弹无需等待首次 AI；Shoot 后的快照已包含模组追加的 extraUpdates 等参数。
            => type.GetMethod(nameof(ModProjectile.PreAI), Type.EmptyTypes)?.DeclaringType == typeof(ModProjectile) &&
                type.GetMethod(nameof(ModProjectile.AI), Type.EmptyTypes)?.DeclaringType == typeof(ModProjectile) &&
                type.GetMethod(nameof(ModProjectile.PostAI), Type.EmptyTypes)?.DeclaringType == typeof(ModProjectile) &&
                type.GetMethod(nameof(ModProjectile.ShouldUpdatePosition), Type.EmptyTypes)?.DeclaringType == typeof(ModProjectile);

        private static AttackAdapter CreateAttackAdapter(Type type)
        {
            string name = type.FullName;
            if (CreateRangedAdapter(type) is { } ranged) return ranged;
            if (name == "CalamityMod.Projectiles.Melee.Spears.BansheeHookProj")
                return GeometryAdapter(type, type, p =>
                {
                    // 镰钩沿朝向反向延伸 95；只回放原线段，不能额外加入矛的矩形判定。
                    float angle = p.rotation - MathHelper.PiOver4 * Math.Sign(p.velocity.X) +
                        (p.spriteDirection == -1 ? MathHelper.Pi : 0);
                    return RectangleFrame(p) with { HasRectangle = false, HasLine = true,
                        LineStart = p.Center, LineEnd = p.Center - angle.ToRotationVector2() * 95,
                        LineWidth = 23 * p.scale, DrawSprite = false };
                });
            if (name == "CalamityMod.Projectiles.Magic.DarkSparkPrism") return new() { Emitter = true };
            if (name == "CalamityMod.Projectiles.Magic.AcidGunStream")
                // 同组引用留在原攻击上；副本跟随各原实例，不执行会销毁原射流的 OnKill。
                return new() { OwnerRelative = false, Capture = p => RectangleFrame(p) with {
                    DrawSprite = false, SpriteRotation = p.velocity.ToRotation() } };
            if (name == "CalamityMod.Projectiles.Magic.DarkSparkBeam")
                return new() { ClipBeam = true, Capture = p => BeamFrame(p, 22 * p.scale, true) };
            if (name == "CalamityMod.Projectiles.Magic.HolyLaser")
                return new() { Capture = p => BeamFrame(p, p.ai[2] == 1 ? 50 : 20, false) };
            for (Type family = type; family != null; family = family.BaseType)
            {
                switch (family.FullName)
                {
                    case CalamityBases + "BaseGunHoldoutProjectile":
                        return GunAdapter(type, family);
                    case CalamityBases + "BaseIdleHoldoutProjectile":
                        if (name == "CalamityMod.Projectiles.Ranged.HeavenlyGaleProj") return new() { Emitter = true };
                        AttackAdapter idle = GeometryAdapter(type, typeof(ModProjectile), RectangleFrame);
                        if (idle.Capture != null) idle.Emitter = true;
                        return idle;
                    case CalamityBases + "BaseSpearProjectile":
                        return GeometryAdapter(type, typeof(ModProjectile), RectangleFrame, originAtCorner: true);
                    case CalamityBases + "BaseShortswordProjectile":
                        return GeometryAdapter(type, family, p =>
                        {
                            Vector2 end = p.Center + p.velocity * (12 * p.scale);
                            return RectangleFrame(p) with { HasRectangle = false, HasLine = true,
                                LineStart = p.Center, LineEnd = end, LineWidth = 24 };
                        });
                    case CalamityBases + "BaseSwordHoldoutProjectile":
                        PropertyInfo length = family.GetProperty("lineCollisionLength");
                        if (length?.PropertyType != typeof(float)) return Unsupported();
                        return GeometryAdapter(type, family, p =>
                        {
                            TimeEchoProjectionFrame frame = RectangleFrame(p);
                            float distance = (float)length.GetValue(p.ModProjectile);
                            Player owner = Main.player[p.owner];
                            Vector2 arm = owner.MountedCenter - new Vector2(5 * owner.direction, 2);
                            return frame with { HasLine = distance > 0, LineStart = p.Center,
                                LineEnd = p.Center + (p.Center - arm).SafeNormalize(Vector2.Zero) * (distance * 0.5f * p.scale) };
                        });
                    case CalamityBases + "BaseCustomUseStyleProjectile":
                        PropertyInfo origin = family.GetProperty("SpriteOrigin"), rotation = family.GetProperty("FinalRotation");
                        PropertyInfo frameCount = family.GetProperty("FrameCount");
                        FieldInfo spriteFrame = family.GetField("Frame");
                        if (origin?.PropertyType != typeof(Vector2) || rotation?.PropertyType != typeof(float) ||
                            spriteFrame?.FieldType != typeof(int) || frameCount?.PropertyType != typeof(int)) return Unsupported();
                        return GeometryAdapter(type, typeof(ModProjectile), p => RectangleFrame(p) with {
                            SpriteOrigin = (Vector2)origin.GetValue(p.ModProjectile),
                            SpriteRotation = (float)rotation.GetValue(p.ModProjectile),
                            SpriteFrame = (int)spriteFrame.GetValue(p.ModProjectile),
                            SpriteFrameCount = (int)frameCount.GetValue(p.ModProjectile) });
                    case CalamityBases + "BaseLaserbeamProjectile":
                        AttackAdapter beam = GeometryAdapter(type, family, p => BeamFrame(p, p.Size.Length() * p.scale, true));
                        // 基类默认不绑定玩家；枪口发出的定点光束不能随之后的玩家移动漂移。
                        beam.OwnerRelative = type.GetMethod("AttachToSomething", Type.EmptyTypes)?.DeclaringType != family;
                        return beam;
                }
            }
            return null;
        }

        private static AttackAdapter Unsupported() => new() { Capture = null };

        private static AttackAdapter GunAdapter(Type type, Type family)
        {
            if (type.GetMethod(nameof(ModProjectile.CanDamage), Type.EmptyTypes)?.DeclaringType == family)
                return new() { Emitter = true };
            // 圆形实现由适配器明确解释；不把未知自定义碰撞近似成矩形。
            if (type.FullName == "CalamityMod.Projectiles.Ranged.HellbornHoldout")
                return new() { Emitter = true, Capture = p => CircleFrame(p, p.Center, 100) };
            if (type.FullName == "CalamityMod.Projectiles.Ranged.M1GarandHoldout")
                return MuzzleFlashAdapter(type, 0.5f, 64);
            AttackAdapter adapter = GeometryAdapter(type, typeof(ModProjectile), RectangleFrame);
            if (adapter.Capture != null) adapter.Emitter = true;
            return adapter;
        }

        private static AttackAdapter MuzzleFlashAdapter(Type type, float offset, float radius)
        {
            PropertyInfo tip = type.GetProperty("GunTipPosition");
            FieldInfo flash = type.GetField("MuzzleFlash", BindingFlags.Public | BindingFlags.Static);
            if (tip?.PropertyType != typeof(Vector2) || flash?.FieldType != typeof(Asset<Texture2D>)) return Unsupported();
            return new() { Emitter = true, Capture = p =>
            {
                TimeEchoProjectionFrame frame = CircleFrame(p, p.Center, radius);
                // 非伤害时段无需读取枪口贴图；贴图未就绪也不会生成错误的圆形判定。
                if (!frame.Damaging) return frame;
                if (flash.GetValue(null) is not Asset<Texture2D> texture)
                    throw new InvalidOperationException("Muzzle flash texture unavailable");
                Vector2 center = (Vector2)tip.GetValue(p.ModProjectile) +
                    p.rotation.ToRotationVector2() * texture.Value.Width * offset;
                return frame with { CircleCenter = center };
            } };
        }

        private static AttackAdapter GeometryAdapter(Type type, Type collisionOwner,
            Func<Projectile, TimeEchoProjectionFrame> capture, bool originAtCorner = false)
        {
            // 未解释的碰撞覆盖不降级成矩形，也不回退执行原 AI。
            if (type.GetMethod(nameof(ModProjectile.Colliding), new[] { typeof(Rectangle), typeof(Rectangle) })?.DeclaringType != collisionOwner)
                return Unsupported();
            return new() { Capture = p => {
                TimeEchoProjectionFrame frame = capture(p);
                return originAtCorner ? frame with { SpriteOrigin = Vector2.Zero } : frame;
            } };
        }

        private static TimeEchoProjectionFrame RectangleFrame(Projectile p)
        {
            Rectangle hitbox = p.Hitbox;
            // 在已识别的实现上读取原实体的碰撞快照；不克隆 ModProjectile，也不调用其 AI/命中/绘制。
            ProjectileLoader.ModifyDamageHitbox(p, ref hitbox);
            return new(p.friendly && !p.hostile && p.damage > 0 && ProjectileLoader.CanDamage(p) != false,
                true, hitbox.Center.ToVector2(), hitbox.Size() * 0.5f, 0,
                false, p.Center, p.Center, 0, p.Center, p.rotation, p.scale, p.frame, Math.Max(1, Main.projFrames[p.type]), p.spriteDirection,
                p.Size * 0.5f, true, Main.player[p.owner].Center, p.ownerHitCheck,
                p.type, p.usesIDStaticNPCImmunity ? Math.Max(0, p.idStaticNPCHitCooldown) : 0);
        }

        private static TimeEchoProjectionFrame BeamFrame(Projectile p, float width, bool rectangle)
            => RectangleFrame(p) with { HasRectangle = rectangle, HasLine = true,
                LineStart = p.Center, LineEnd = p.Center + p.velocity * p.localAI[1], LineWidth = width, DrawSprite = false };

        private static TimeEchoProjectionFrame CircleFrame(Projectile p, Vector2 center, float radius)
            => RectangleFrame(p) with { HasRectangle = false, HasLine = false,
                HasCircle = true, CircleCenter = center, CircleRadius = radius };

        internal static bool TryCaptureProjection(Projectile p, AttackAdapter adapter, out TimeEchoProjectionFrame frame)
        {
            frame = default;
            if (adapter.Failed) return false;
            if (adapter.Capture == null)
            {
                // 未适配类型是预期边界，不抛异常触发 tML 的静默异常日志。
                adapter.Failed = true;
                ModContent.GetInstance<TimeEchoAttackSystem>().Mod.Logger.Warn(
                    $"TimeEcho geometry adapter disabled for {p.ModProjectile?.FullName}: unrecognized collision override or adapter members; original attack is unchanged.");
                return false;
            }
            try
            {
                frame = adapter.Capture(p);
                if (!frame.IsValid) throw new InvalidOperationException("Invalid attack geometry");
                return true;
            }
            catch (Exception exception)
            {
                adapter.Failed = true;
                ModContent.GetInstance<TimeEchoAttackSystem>().Mod.Logger.Warn(
                    $"TimeEcho geometry adapter disabled for {p.ModProjectile?.FullName}; original attack is unchanged.", exception);
                return false;
            }
        }
    }
}
