using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Projectiles.Minions
{
    public class BlackHoleMinion : ModProjectile
    {
        // ==================== 轨道参数 ====================
        // 最多同时存在的黑洞数量。这里要和 BlackHoleStaff 里的上限保持一致。
        private const int MaxBlackHoles = 4;

        // 黑洞绕“游走中心”旋转的椭圆半径。
        // X 控制左右距离，Y 控制上下距离；透镜仍然离玩家太近时优先调大这两个值。
        private const float OrbitRadiusX = 730f;
        private const float OrbitRadiusY = 490f;

        // 黑洞绕圈速度。数值越小越慢，越像沉重天体；数值越大越像高速法阵。
        private const float OrbitSpeed = 0.012f;

        // 游走中心相对玩家的可移动范围。
        // 黑洞不是绕玩家转，而是绕这个随机游走的中心转；这两个值越大，整体星环越飘。
        private const float WanderRangeX = 780f;
        private const float WanderRangeY = 600f;

        // 游走中心每隔多少帧重新选一个随机目标点。
        // 最小/最大值组成随机区间，60 帧约等于 1 秒。
        private const int WanderTargetRetargetTimeMin = 90;
        private const int WanderTargetRetargetTimeMax = 150;

        // 游走中心追向目标点的平滑速度。越大越灵敏，越小越漂浮。
        private const float WanderCenterLerp = 0.025f;

        // 黑洞追向轨道点的力度。越大越贴合轨道，越小越有惯性。
        private const float OrbitFollowStrength = 0.06f;

        // 黑洞速度向目标速度靠拢的平滑量。越大响应越快。
        private const float OrbitVelocityLerp = 0.11f;

        // 黑洞最大移动速度，防止游走中心或玩家快速移动时黑洞瞬移感过强。
        private const float MaxOrbitSpeed = 15f;

        // 黑洞离玩家超过这个距离时，直接传送回轨道附近，避免丢失。
        private const float ReturnDistance = 2200f;

        // ==================== 黑洞视觉参数 ====================
        // 传给现有引力透镜系统的半径，同时影响黑洞视觉大小和透镜范围。
        private const float BlackHoleVisualRadius = 24f;

        // 吸积盘颜色。这里只影响现有黑洞 shader 的亮环颜色。
        private static readonly Color AccretionDiskColor = new(95, 170, 255);

        // ==================== 敌方弹幕吸收参数 ====================
        // 敌方弹幕进入这个半径后会被轻微拉向黑洞，但还不会立刻消失。
        private const float ProjectilePullRadius = 320f;

        // 敌方弹幕进入这个半径后会被黑洞直接吞掉。可以比视觉黑洞大几倍，表现强引力。
        private const float ProjectileAbsorbRadius = 120f;

        // 拉拽敌弹的力度。越大敌弹转向越明显，太大会显得突兀。
        private const float ProjectilePullStrength = 1f;

        // 每个黑洞每帧最多处理多少个敌弹，避免密集弹幕时性能波动。
        private const int MaxProjectilePullsPerFrame = 16;

        // ==================== 类星体喷流参数 ====================
        // 黑洞索敌范围。没有目标时不会发射。
        private const float TargetSearchRange = 1200f;

        // 每个黑洞的基础开火间隔，单位是帧。
        private const int QuasarFireRate = 42;

        // 多个黑洞之间的开火错峰帧数，避免所有黑洞同一帧齐射。
        private const int QuasarFireOffsetPerIndex = 8;

        // 喷流从吸积盘边缘射出。这个值控制发射点离黑洞中心多远。
        private const float QuasarEmissionRadius = 48f;

        // 发射口沿吸积盘高速旋转，制造狂暴活动感。
        private const float QuasarDiskSpinSpeed = 0.22f;

        // 喷流主要沿吸积盘切线甩出，只轻微偏向目标，不进行追踪。
        private const float QuasarTargetBias = 0.18f;
        private const float QuasarSpread = 0.16f;

        // 类星体喷流射弹的初速度。
        private const float QuasarShotSpeed = 66f;

        private Vector2 orbitCenter;

        private Player Owner => Main.player[Projectile.owner];
        private BlackHoleMinionPlayer ModdedOwner => Owner.GetModPlayer<BlackHoleMinionPlayer>();

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 1;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override void AI()
        {
            CheckActive();

            NPC target = FindTarget();
            GetOrbitIndex(out int index, out int total);
            total = Math.Min(total, MaxBlackHoles);

            orbitCenter = UpdateSharedWanderingOrbitCenter();

            if (Projectile.Distance(Owner.Center) > ReturnDistance)
            {
                Projectile.Center = orbitCenter + GetOrbitOffset(index, Math.Max(total, 1));
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
            }

            MoveToOrbit(orbitCenter + GetOrbitOffset(index, Math.Max(total, 1)));
            AbsorbHostileProjectiles();
            TryShootQuasarJet(target, index);

            if (!Main.dedServ)
                GravitationalLensSystem.RegisterBlackHole(Projectile.Center, BlackHoleVisualRadius, 1f, AccretionDiskColor, GetCoreTexture());

            Projectile.rotation += 0.035f;
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
            Lighting.AddLight(Projectile.Center, 0.15f, 0.28f, 0.65f);
        }

        private void CheckActive()
        {
            Owner.AddBuff(ModContent.BuffType<BlackHoleMinionBuff>(), 2);

            if (Owner.dead || !Owner.active)
            {
                ModdedOwner.blackHoleMinion = false;
                return;
            }

            if (ModdedOwner.blackHoleMinion)
                Projectile.timeLeft = 2;
        }

        private NPC FindTarget()
        {
            if (Owner.HasMinionAttackTargetNPC)
            {
                NPC forced = Main.npc[Owner.MinionAttackTargetNPC];
                if (forced.CanBeChasedBy(Projectile) && Projectile.Distance(forced.Center) <= TargetSearchRange * 1.5f)
                    return forced;
            }

            NPC best = null;
            float bestDistanceSq = TargetSearchRange * TargetSearchRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float distanceSq = Vector2.DistanceSquared(npc.Center, Projectile.Center);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                best = npc;
            }

            return best;
        }

        private Vector2 UpdateSharedWanderingOrbitCenter()
        {
            if (ModdedOwner.BlackHoleOrbitLastUpdateTick == Main.GameUpdateCount)
                return ModdedOwner.BlackHoleOrbitCenter;

            ModdedOwner.BlackHoleOrbitLastUpdateTick = Main.GameUpdateCount;

            if (ModdedOwner.BlackHoleOrbitCenter == Vector2.Zero)
            {
                ModdedOwner.BlackHoleOrbitCenter = Owner.Center + PickWanderOffset();
                ModdedOwner.BlackHoleOrbitTarget = Owner.Center + PickWanderOffset();
                ModdedOwner.BlackHoleOrbitTargetTimer = Main.rand.Next(WanderTargetRetargetTimeMin, WanderTargetRetargetTimeMax + 1);
            }

            ModdedOwner.BlackHoleOrbitTargetTimer--;
            float distanceToTarget = Vector2.Distance(ModdedOwner.BlackHoleOrbitCenter, ModdedOwner.BlackHoleOrbitTarget);

            if (ModdedOwner.BlackHoleOrbitTargetTimer <= 0 || distanceToTarget < 24f)
            {
                ModdedOwner.BlackHoleOrbitTarget = Owner.Center + PickWanderOffset();
                ModdedOwner.BlackHoleOrbitTargetTimer = Main.rand.Next(WanderTargetRetargetTimeMin, WanderTargetRetargetTimeMax + 1);
            }

            Vector2 clampedTarget = ClampToWanderEllipse(ModdedOwner.BlackHoleOrbitTarget, Owner.Center);
            ModdedOwner.BlackHoleOrbitCenter = Vector2.Lerp(ModdedOwner.BlackHoleOrbitCenter, clampedTarget, WanderCenterLerp);
            ModdedOwner.BlackHoleOrbitCenter = ClampToWanderEllipse(ModdedOwner.BlackHoleOrbitCenter, Owner.Center);
            return ModdedOwner.BlackHoleOrbitCenter;
        }

        private static Vector2 PickWanderOffset()
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = MathF.Sqrt(Main.rand.NextFloat());
            return new Vector2(MathF.Cos(angle) * WanderRangeX, MathF.Sin(angle) * WanderRangeY) * radius;
        }

        private static Vector2 ClampToWanderEllipse(Vector2 position, Vector2 playerCenter)
        {
            Vector2 offset = position - playerCenter;
            if (offset == Vector2.Zero)
                return position;

            float normalized = offset.X * offset.X / (WanderRangeX * WanderRangeX)
                + offset.Y * offset.Y / (WanderRangeY * WanderRangeY);

            if (normalized <= 1f)
                return position;

            return playerCenter + offset / MathF.Sqrt(normalized);
        }

        private void GetOrbitIndex(out int index, out int total)
        {
            index = 0;
            total = 0;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner || other.type != Projectile.type)
                    continue;

                if (other.whoAmI == Projectile.whoAmI)
                    index = total;

                total++;
            }
        }

        private static Vector2 GetOrbitOffset(int index, int total)
        {
            float time = Main.GameUpdateCount * OrbitSpeed;
            float phase = MathHelper.TwoPi * index / total;

            if (total == 2)
                phase += MathHelper.PiOver2;
            else if (total == 4)
                phase += MathHelper.PiOver4;

            float angle = time + phase;
            Vector2 ellipse = new(MathF.Cos(angle) * OrbitRadiusX, MathF.Sin(angle) * OrbitRadiusY);
            float breathe = 1f + MathF.Sin(time * 1.7f + phase) * 0.04f;
            return ellipse * breathe;
        }

        private void MoveToOrbit(Vector2 targetPosition)
        {
            Vector2 desiredVelocity = (targetPosition - Projectile.Center) * OrbitFollowStrength;

            if (desiredVelocity.Length() > MaxOrbitSpeed)
                desiredVelocity = desiredVelocity.SafeNormalize(Vector2.Zero) * MaxOrbitSpeed;

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, OrbitVelocityLerp);
        }

        private void AbsorbHostileProjectiles()
        {
            int handled = 0;
            float pullRadiusSq = ProjectilePullRadius * ProjectilePullRadius;
            float absorbRadiusSq = ProjectileAbsorbRadius * ProjectileAbsorbRadius;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile hostile = Main.projectile[i];
                if (!hostile.active || !hostile.hostile || hostile.friendly || hostile.damage <= 0)
                    continue;

                var globalProjectile = hostile.GetGlobalProjectile<global::TestMod.Common.GlobalProjectiles.GlobalProjectile>();
                if (globalProjectile.AlreadyReflected)
                    continue;

                Vector2 toBlackHole = Projectile.Center - hostile.Center;
                float distanceSq = toBlackHole.LengthSquared();

                if (distanceSq > pullRadiusSq)
                    continue;

                Vector2 pullDirection = toBlackHole.SafeNormalize(Vector2.Zero);

                if (distanceSq <= absorbRadiusSq)
                {
                    globalProjectile.AlreadyReflected = true;
                    hostile.Kill();
                    handled++;
                }
                else
                {
                    float distance = MathF.Sqrt(distanceSq);
                    float pullFactor = 1f - distance / ProjectilePullRadius;
                    hostile.velocity = Vector2.Lerp(hostile.velocity, hostile.velocity + pullDirection * ProjectilePullStrength * (0.35f + pullFactor), 0.45f);
                    handled++;
                }

                if (handled >= MaxProjectilePullsPerFrame)
                    return;
            }
        }

        private void TryShootQuasarJet(NPC target, int index)
        {
            if (target is null || Main.myPlayer != Projectile.owner)
                return;

            int timer = (int)Projectile.localAI[0]++;
            if ((timer + index * QuasarFireOffsetPerIndex) % QuasarFireRate != 0)
                return;

            float diskAngle = Main.GameUpdateCount * QuasarDiskSpinSpeed
                + Projectile.whoAmI * 0.73f
                + index * MathHelper.PiOver2;
            Vector2 diskRadial = diskAngle.ToRotationVector2();
            Vector2 tangent = diskRadial.RotatedBy(MathHelper.PiOver2);
            Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(tangent);

            if (Vector2.Dot(tangent, toTarget) < 0f)
                tangent = -tangent;

            Vector2 direction = Vector2.Lerp(tangent, toTarget, QuasarTargetBias)
                .RotatedBy(Main.rand.NextFloat(-QuasarSpread, QuasarSpread))
                .SafeNormalize(tangent);
            Vector2 spawnPosition = Projectile.Center + diskRadial * QuasarEmissionRadius;

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPosition,
                direction * QuasarShotSpeed,
                ModContent.ProjectileType<QuasarJetProjectile>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner);
        }

        public override bool? CanDamage() => false;

        public override bool MinionContactDamage() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        private Texture2D GetCoreTexture() => null;
        //private Texture2D GetCoreTexture()
        //    => TextureAssets.Projectile[Type].Value;
    }
}
