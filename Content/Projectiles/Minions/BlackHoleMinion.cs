using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Common.Graphics.DynamicText;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Projectiles.Minions
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
        private const int WanderTargetRetargetTimeMax = 210;

        // 游走中心追向目标点的平滑速度。越大越灵敏，越小越漂浮。
        private const float WanderCenterLerp = 0.025f;

        // 黑洞追向轨道点的力度。越大越贴合轨道，越小越有惯性。
        private const float OrbitFollowStrength = 0.06f;

        // 黑洞速度向目标速度靠拢的平滑量。越大响应越快。
        private const float OrbitVelocityLerp = 0.11f;

        // 黑洞最大移动速度，防止游走中心或玩家快速移动时黑洞瞬移感过强。
        private const float MaxOrbitSpeed = 15f;

        // 黑洞离玩家超过这个距离时，直接传送回轨道附近，避免丢失。
        private const float ReturnDistance = 1700f;

        // ==================== 黑洞视觉参数 ====================
        // 传给现有引力透镜系统的半径，同时影响黑洞视觉大小和透镜范围。
        private const float BlackHoleVisualRadius = 32f;

        // 吸积盘颜色。这里只影响现有黑洞 shader 的亮环颜色。
        private static readonly Color AccretionDiskColor = new(255, 132, 48);
        private const float ForegroundDiskTextureScale = 0.92f;
        private const float ForegroundDiskOpacity = 0.72f;

        // ==================== 敌方弹幕吸收参数 ====================
        // 敌方弹幕进入这个半径后会被轻微拉向黑洞，但还不会立刻消失。
        private const float ProjectilePullRadius = 400f;

        // 敌方弹幕进入这个半径后会被黑洞直接吞掉。可以比视觉黑洞大几倍，表现强引力。
        private const float ProjectileAbsorbRadius = 130f;

        // 拉拽敌弹的力度。越大敌弹转向越明显，太大会显得突兀。
        private const float ProjectilePullStrength = 2f;

        // 每个黑洞每帧最多处理多少个敌弹，避免密集弹幕时性能波动。
        private const int MaxProjectilePullsPerFrame = 20;

        // ==================== 类星体喷流参数 ====================
        // 临时测试开关：关闭后黑洞只保留吸积盘扣血和敌弹吸收，不再发射喷流/碎片弹幕。
        private static readonly bool EnableQuasarProjectiles = true;

        // 黑洞索敌范围。射弹无目标时也会随机发射；喷流仍只在有目标时蓄力发射。
        private const float TargetSearchRange = 1800f;

        // 单发射弹不规则发射间隔。60 帧约等于 1 秒。
        private const int QuasarIdleFireIntervalMin = 5 * 60;
        private const int QuasarIdleFireIntervalMax = 10 * 60;
        private const int QuasarLockedFireIntervalMin = 1 * 60;
        private const int QuasarLockedFireIntervalMax = 5 * 60;
        private const int QuasarProjectileFireOffsetPerIndex = 18;

        // 喷流蓄力时间。蓄力期间只显示吸积盘聚能，不立刻造成伤害。
        private const int QuasarChargeTime = 60;

        // 喷流持续时间。真正的命中范围由 QuasarJetProjectile 的长线段碰撞控制。
        private const int QuasarBeamDuration = 120;

        // 每次喷流结束后的冷却时间。
        private const int QuasarCooldown = 210;

        // 多个黑洞之间的开火错峰帧数，避免所有黑洞同一帧齐射。
        private const int QuasarFireOffsetPerIndex = 30;

        // 喷流从吸积盘边缘射出。这个值控制发射点离黑洞中心多远。
        private const float QuasarEmissionRadius = 48f;

        // ==================== 吸积盘核心扣血参数 ====================
        // 以吸积盘为圆形范围，范围内直接流失生命，不走撞击/命中判定。
        private const float AccretionDiskDrainRadius = BlackHoleVisualRadius * GravitationalLensSystem.AccretionOuterRadiusFactor;

        // 扣血间隔。数值越小文本越密、伤害越平滑；数值越大越像周期脉冲。
        private const int AccretionDiskDrainInterval = 2;

        // 每秒按 NPC 最大生命值扣除的比例。越靠近圆心越接近中心比例，越靠近边缘越接近边缘比例。
        private const float AccretionDiskEdgeDrainLifeMaxPercentPerSecond = 0.06f;
        private const float AccretionDiskCenterDrainLifeMaxPercentPerSecond = 0.17f;

        // 每次结算至少扣多少血，避免低血量小怪被百分比向下取整到 0。
        private const int AccretionDiskMinDrainDamage = 100;

        // 单发碎片射弹参数。
        private const float QuasarBurstShardDamageFactor = 13f;
        private const float QuasarBurstShardSpeed = 86f;
        private const float QuasarBurstShardSpread = 0.32f;

        // 蓄力点沿吸积盘高速旋转，制造狂暴活动感。
        private const float QuasarDiskSpinSpeed = 0.22f;

        private Vector2 orbitCenter;
        private int quasarProjectileTimer;
        private bool quasarProjectileTimerInitialized;

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
            Projectile.minionSlots = 9f;
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
            DrainNPCsInAccretionDisk();

            if (EnableQuasarProjectiles)
            {
                TryShootQuasarProjectile(target, index);
                TryShootQuasarJet(target, index);
            }
            else
            {
                Projectile.localAI[0] = 0f;
                Projectile.localAI[1] = 0f;
                quasarProjectileTimer = 0;
                quasarProjectileTimerInitialized = false;
            }

            if (!Main.dedServ)
                GravitationalLensSystem.RegisterBlackHole(Projectile.Center, BlackHoleVisualRadius, 1f, AccretionDiskColor, GetVisualStyle());

            Projectile.rotation += 0.035f;
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
            Lighting.AddLight(Projectile.Center, 0.15f, 0.28f, 0.65f);
        }

        private void CheckActive()
        {
            if (Owner.dead || !Owner.active)
            {
                ModdedOwner.blackHoleMinion = false;
                return;
            }

            int buffType = ModContent.BuffType<BlackHoleMinionBuff>();
            bool shouldStayAlive = Owner.HasBuff(buffType) || ModdedOwner.blackHoleMinion;

            if (shouldStayAlive)
            {
                Owner.AddBuff(buffType, 2);
                ModdedOwner.blackHoleMinion = true;
                Projectile.timeLeft = 2;
            }
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

        private void DrainNPCsInAccretionDisk()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if ((Main.GameUpdateCount + (uint)Projectile.whoAmI) % AccretionDiskDrainInterval != 0)
                return;

            float drainRadiusSq = AccretionDiskDrainRadius * AccretionDiskDrainRadius;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!CanBeDrainedByAccretionDisk(npc))
                    continue;

                float distanceSq = DistanceFromPointToRectangleSquared(Projectile.Center, npc.Hitbox);
                if (distanceSq > drainRadiusSq)
                    continue;

                float distance = MathF.Sqrt(distanceSq);
                float centerCloseness = 1f - MathHelper.Clamp(distance / AccretionDiskDrainRadius, 0f, 1f);
                float drainPercentPerSecond = MathHelper.Lerp(
                    AccretionDiskEdgeDrainLifeMaxPercentPerSecond,
                    AccretionDiskCenterDrainLifeMaxPercentPerSecond,
                    centerCloseness);

                int damage = Math.Max(
                    AccretionDiskMinDrainDamage,
                    (int)MathF.Round(npc.lifeMax * drainPercentPerSecond * AccretionDiskDrainInterval / 60f));

                damage = Math.Min(damage, npc.life);
                if (damage <= 0)
                    continue;

                npc.life -= damage;
                npc.lastInteraction = Projectile.owner;
                ShowAccretionDiskDrainText(npc, damage);

                if (npc.life <= 0)
                    npc.checkDead();

                npc.netUpdate = true;
            }
        }

        private static bool CanBeDrainedByAccretionDisk(NPC npc)
            => npc.active
            && !npc.friendly
            && !npc.dontTakeDamage
            && npc.life > 0
            && npc.lifeMax > 0;

        private static float DistanceFromPointToRectangleSquared(Vector2 point, Rectangle rectangle)
        {
            float closestX = MathHelper.Clamp(point.X, rectangle.Left, rectangle.Right);
            float closestY = MathHelper.Clamp(point.Y, rectangle.Top, rectangle.Bottom);
            return Vector2.DistanceSquared(point, new Vector2(closestX, closestY));
        }

        private static void ShowAccretionDiskDrainText(NPC npc, int damage)
        {
            Rectangle hitbox = npc.Hitbox;
            Color textColor = Color.DarkGoldenrod;

            if (Main.dedServ)
            {
                NetMessage.SendData(MessageID.CombatTextInt, -1, -1, null, (int)textColor.PackedValue, hitbox.Center.X, hitbox.Center.Y, damage);
                return;
            }

            DynamicWorldTextSystem.SpawnCombatText(hitbox, damage, true, textColor, DynamicTextStyleRegistry.BlackHoleAbsorb);
        }

        private void TryShootQuasarProjectile(NPC target, int index)
        {
            bool hasTarget = target is not null;
            if (!quasarProjectileTimerInitialized)
            {
                quasarProjectileTimerInitialized = true;
                ResetQuasarFireTimer(hasTarget, index);
            }

            if (hasTarget && quasarProjectileTimer > QuasarLockedFireIntervalMax + index * QuasarProjectileFireOffsetPerIndex)
                quasarProjectileTimer = Main.rand.Next(QuasarLockedFireIntervalMin, QuasarLockedFireIntervalMax + 1) + index * QuasarProjectileFireOffsetPerIndex;

            if (quasarProjectileTimer > 0)
            {
                quasarProjectileTimer--;
                return;
            }

            Vector2 direction = PickQuasarProjectileDirection(target);
            Vector2 spawnPosition = Projectile.Center + direction * QuasarEmissionRadius;
            SpawnQuasarChargeEffects(direction, 1f, index);
            ResetQuasarFireTimer(hasTarget, index);

            if (Main.myPlayer != Projectile.owner)
                return;

            ShootQuasarBurstShard(spawnPosition, direction, target?.whoAmI ?? -1);
        }

        private void ResetQuasarFireTimer(bool hasTarget, int index)
        {
            int min = hasTarget ? QuasarLockedFireIntervalMin : QuasarIdleFireIntervalMin;
            int max = hasTarget ? QuasarLockedFireIntervalMax : QuasarIdleFireIntervalMax;
            quasarProjectileTimer = Main.rand.Next(min, max + 1) + index * QuasarProjectileFireOffsetPerIndex;
        }

        private Vector2 PickQuasarProjectileDirection(NPC target)
        {
            Vector2 direction = target is not null
                ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                : Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();

            return direction.RotatedByRandom(QuasarBurstShardSpread).SafeNormalize(Vector2.UnitX);
        }

        private void TryShootQuasarJet(NPC target, int index)
        {
            if (Projectile.localAI[0] > 0f)
                Projectile.localAI[0]--;

            if (target is null)
            {
                Projectile.localAI[1] = 0f;
                return;
            }

            if (Projectile.localAI[0] > 0f)
            {
                Projectile.localAI[1] = 0f;
                return;
            }

            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Projectile.localAI[1]++;

            int chargeTime = QuasarChargeTime + index * QuasarFireOffsetPerIndex;
            float chargeProgress = MathHelper.Clamp(Projectile.localAI[1] / chargeTime, 0f, 1f);
            SpawnQuasarChargeEffects(direction, chargeProgress, index);

            if (Projectile.localAI[1] < chargeTime)
                return;

            Projectile.localAI[1] = 0f;
            Projectile.localAI[0] = QuasarCooldown + index * QuasarFireOffsetPerIndex;

            if (Main.myPlayer != Projectile.owner)
                return;

            ShootQuasarBeam(direction, 1f);
            ShootQuasarBeam(-direction, -1f);
        }

        private void ShootQuasarBeam(Vector2 direction, float parentDirectionSign)
        {
            int beam = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center + direction * QuasarEmissionRadius,
                Vector2.Zero,
                ModContent.ProjectileType<QuasarJetProjectile>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                direction.ToRotation(),
                QuasarBeamDuration,
                (Projectile.whoAmI + 1) * parentDirectionSign);

            if (beam >= 0 && beam < Main.maxProjectiles)
                Main.projectile[beam].originalDamage = Projectile.originalDamage > 0 ? Projectile.originalDamage : Projectile.damage;
        }

        private void ShootQuasarBurstShard(Vector2 spawnPosition, Vector2 shardDirection, int targetIndex)
        {
            int shardDamage = Math.Max(1, (int)MathF.Round(Projectile.damage * QuasarBurstShardDamageFactor));
            int shardOriginalDamage = Math.Max(1, (int)MathF.Round((Projectile.originalDamage > 0 ? Projectile.originalDamage : Projectile.damage) * QuasarBurstShardDamageFactor));

            int shard = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPosition,
                shardDirection * QuasarBurstShardSpeed,
                ModContent.ProjectileType<QuasarJetBurstShard>(),
                shardDamage,
                Projectile.knockBack,
                Projectile.owner,
                targetIndex + 1);

            if (shard >= 0 && shard < Main.maxProjectiles)
                Main.projectile[shard].originalDamage = shardOriginalDamage;
        }

        private void SpawnQuasarChargeEffects(Vector2 direction, float chargeProgress, int index)
        {
            if (Main.dedServ)
                return;

            float diskAngle = Main.GameUpdateCount * QuasarDiskSpinSpeed
                + Projectile.whoAmI * 0.73f
                + index * MathHelper.PiOver2;
            Vector2 rotatingPoint = diskAngle.ToRotationVector2();
            Vector2 emissionPoint = Projectile.Center + direction * QuasarEmissionRadius;
            Vector2 ringPoint = Projectile.Center + rotatingPoint * QuasarEmissionRadius * MathHelper.Lerp(0.72f, 1.05f, chargeProgress);
            Vector2 pullDirection = (emissionPoint - ringPoint).SafeNormalize(direction);
            float dustScale = MathHelper.Lerp(0.75f, 1.75f, chargeProgress);

            if (Main.rand.NextFloat() < MathHelper.Lerp(0.35f, 0.9f, chargeProgress))
            {
                Dust dust = Dust.NewDustPerfect(
                    ringPoint,
                    DustID.Electric,
                    pullDirection * Main.rand.NextFloat(1.2f, 3.6f),
                    70,
                    Color.Lerp(AccretionDiskColor, Color.White, chargeProgress * 0.55f),
                    dustScale);
                dust.noGravity = true;
            }

            Lighting.AddLight(emissionPoint, 0.12f + chargeProgress * 0.3f, 0.26f + chargeProgress * 0.45f, 0.58f + chargeProgress * 0.8f);
        }

        public override bool? CanDamage() => false;

        public override bool MinionContactDamage() => false;

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = texture.Frame();
            Vector2 origin = frame.Size() * 0.5f;
            float visualDiameter = BlackHoleVisualRadius * GravitationalLensSystem.AccretionOuterRadiusFactor * 2f;
            float scale = visualDiameter / Math.Max(frame.Width, 1) * ForegroundDiskTextureScale;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                frame,
                Color.White * ForegroundDiskOpacity,
                GetForegroundDiskRotation(),
                origin,
                scale,
                SpriteEffects.None,
                0);

            return false;
        }

        private static float GetForegroundDiskRotation()
            => GravitationalLensSystem.GetAccretionDiskTiltRadians(Main.GlobalTimeWrappedHourly);

        private static BlackHoleVisualStyle GetVisualStyle()
            // 默认仍使用 BlackHoleCore；放入 BlackHoleDisk/BlackHoleDiskFlow 后自动启用精细吸积盘。
            => BlackHoleVisualStyle.DefaultCoreMaterialDisk;
    }
}
