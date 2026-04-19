using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace 武器test
{
    /// <summary>
    /// 全局弹射物钩子：武器强化的核心处理类
    /// 包含追踪增强、伤害修正、命中特效三大功能
    /// </summary>
    public class MyGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // 万花筒连击衰减系数
        public float currentWhipDecay = 1f;

        // ── 星尘龙追踪冷却 ──
        private int _dragonTrackingCooldown = 0;

        // 星尘龙龙头目标锁定
        private int _lockedTargetIndex = -1;
        private int _lockedTargetTimer = 0;
        private const int LOCK_DURATION = 45;

        // 破晓之光太阳爆发触发记录，防止每帧重复触发
        private static readonly HashSet<int> _daybreakBurstFiredSet = new HashSet<int>();

        // ══════════════════════════════════════════════════════════════
        //   PostAI — 每帧追踪入口（按弹射物类型分发到各专属追踪逻辑）
        // ══════════════════════════════════════════════════════════════
        public override void PostAI(Projectile projectile)
        {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;

            Player player = Main.player[projectile.owner];
            if (!player.active) return;

            bool godMode = player.GetModPlayer<MyPlayer>().godModeBuff;
            float summonRange = Math.Max(Main.screenWidth, Main.screenHeight) * 1.3f;

            // 🐉 星尘龙（仅龙头，godMode 开启时生效）
            if (ProjectileID.Sets.StardustDragon[projectile.type])
            {
                if (!godMode || projectile.type != ProjectileID.StardustDragon1) return;
                ApplyStardustDragonTracking(projectile);
            }
            // 🧠 星尘细胞子细胞（godMode 开启时生效）
            else if (projectile.type == ProjectileID.StardustCellMinionShot)
            {
                if (!godMode) return;
                ApplyHighTierTracking(projectile, 51f, 120f, 0.6f, 0.8f);
            }
            // ⚡ 泰拉棱镜
            else if (projectile.type == ProjectileID.EmpressBlade)
            {
                ApplyHighTierTracking(projectile, 18f, 90f, 0.32f, 0.45f);
            }
            // 🐺 乌鸦(1802) + 沙漠虎(4607)
            else if (projectile.type == 1802 || projectile.type == 4607)
            {
                ApplyHighTierTracking(projectile, 40f, 83f, 0.3f, 0.6f);
            }
            // ☀️ 破晓之光矛（仅飞行中追踪，插入敌人后不再干预）
            else if (projectile.type == 636)
            {
                if (!godMode || projectile.ai[0] != 0) return;
                ApplyDaybreakTracking(projectile);
            }
            // 👻 普通召唤物（排除哨兵和星尘细胞本体）
            else if (projectile.minion && !projectile.sentry &&
                     projectile.type != ProjectileID.StardustCellMinion)
            {
                ApplyGenericMinionTracking(projectile, player, summonRange);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   星尘龙专属追踪：激进索敌 + 目标锁定 + 速度预测
        // ══════════════════════════════════════════════════════════════
        private void ApplyStardustDragonTracking(Projectile projectile)
        {
            // ── 如果还在冷却中，递减并直接跳出，靠惯性飞行 ──
            if (_dragonTrackingCooldown > 0)
            {
                _dragonTrackingCooldown--;
                return;
            }

            const float maxRange        = 7200f;
            const float minSpeed        = 42f;
            const float maxSpeed        = 150f;
            const float lerpAmount      = 0.1f;
            const float correctionForce = 0.5f;
            const float breakDistance   = 900f;

            _lockedTargetTimer--;

            // 锁定失效条件：无效目标 / 锁定超时 / 距离过远
            bool needReacquire =
                _lockedTargetIndex < 0 ||
                _lockedTargetIndex >= Main.npc.Length ||
                !Main.npc[_lockedTargetIndex].active ||
                !Main.npc[_lockedTargetIndex].CanBeChasedBy() ||
                _lockedTargetTimer <= 0 ||
                Vector2.Distance(projectile.Center, Main.npc[_lockedTargetIndex].Center) > breakDistance;

            if (needReacquire)
            {
                _lockedTargetIndex = AcquireNearestTarget(projectile.Center, maxRange);
                _lockedTargetTimer = LOCK_DURATION;
            }

            if (_lockedTargetIndex < 0) return;

            NPC target = Main.npc[_lockedTargetIndex];
            Vector2 toTarget = target.Center - projectile.Center;
            float dist = toTarget.Length();
            if (dist < 1f) return;

            // 预测目标未来位置
            float currentSpeed  = Math.Max(projectile.velocity.Length(), 1f);
            float predictFrames = MathHelper.Clamp(dist / currentSpeed, 0f, 25f);
            Vector2 toPredicted = (target.Center + target.velocity * predictFrames * 0.65f) - projectile.Center;
            if (toPredicted.LengthSquared() < 1f) toPredicted = toTarget;
            toPredicted.Normalize();

            float desiredSpeed = MathHelper.Clamp(minSpeed + dist / 30f, minSpeed, maxSpeed);
            projectile.velocity  = Vector2.Lerp(projectile.velocity, toPredicted * desiredSpeed, lerpAmount);
            projectile.velocity += toPredicted * correctionForce;

            // 速度钳制
            float finalSpeed = projectile.velocity.Length();
            if (finalSpeed > maxSpeed + 8f)
                projectile.velocity = projectile.velocity / finalSpeed * (maxSpeed + 8f);

            projectile.netUpdate = true;
        }

        // ══════════════════════════════════════════════════════════════
        //   破晓之光矛追踪：带重力抵消
        // ══════════════════════════════════════════════════════════════
        private void ApplyDaybreakTracking(Projectile projectile)
        {
            const float trackRange      = 800f;
            const float minSpeed        = 36f;
            const float maxSpeed        = 60f;
            const float lerpAmount      = 0.2f;
            const float correctionForce = 0.4f;

            int targetIndex = AcquireNearestTarget(projectile.Center, trackRange);
            if (targetIndex < 0) return;

            NPC target = Main.npc[targetIndex];
            Vector2 toTarget = target.Center - projectile.Center;
            float dist = toTarget.Length();
            if (dist < 1f) return;

            // 预测目标未来位置
            float currentSpeed  = Math.Max(projectile.velocity.Length(), 1f);
            float predictFrames = MathHelper.Clamp(dist / currentSpeed, 0f, 15f);
            Vector2 toPredicted = (target.Center + target.velocity * predictFrames * 0.35f) - projectile.Center;
            if (toPredicted.LengthSquared() < 1f) toPredicted = toTarget;
            toPredicted.Normalize();

            float desiredSpeed = MathHelper.Clamp(minSpeed + dist / 40f, minSpeed, maxSpeed);
            projectile.velocity  = Vector2.Lerp(projectile.velocity, toPredicted * desiredSpeed, lerpAmount);
            projectile.velocity += toPredicted * correctionForce;

            float finalSpeed = projectile.velocity.Length();
            if (finalSpeed > maxSpeed + 5f)
                projectile.velocity = projectile.velocity / finalSpeed * (maxSpeed + 5f);

            projectile.velocity.Y -= 0.3f; // 抵消重力
            projectile.netUpdate = true;
        }

        // ══════════════════════════════════════════════════════════════
        //   通用高阶追踪（泰拉棱镜 / 乌鸦 / 沙漠虎 / 星尘细胞子细胞）
        // ══════════════════════════════════════════════════════════════
        private void ApplyHighTierTracking(Projectile projectile, float minSpeed, float maxSpeed,
            float lerpAmount, float extraCorrection)
        {
            int targetIndex = AcquireNearestTarget(projectile.Center, 2400f);
            if (targetIndex < 0) return;

            NPC target = Main.npc[targetIndex];
            Vector2 toTarget = target.Center - projectile.Center;
            if (toTarget.LengthSquared() <= 1f) return;

            float dist = toTarget.Length();
            toTarget.Normalize();

            float desiredSpeed = MathHelper.Clamp(minSpeed + dist / 45f, minSpeed, maxSpeed);
            projectile.velocity  = Vector2.Lerp(projectile.velocity, toTarget * desiredSpeed, lerpAmount);
            projectile.velocity += toTarget * extraCorrection;
            projectile.netUpdate = true;
        }

        // ══════════════════════════════════════════════════════════════
        //   普通召唤物追踪：以玩家为圆心寻敌
        // ══════════════════════════════════════════════════════════════
        private void ApplyGenericMinionTracking(Projectile projectile, Player player, float maxRange)
        {
            int targetIndex = AcquireNearestTarget(player.Center, maxRange);

            if (targetIndex >= 0)
            {
                NPC target = Main.npc[targetIndex];
                Vector2 toTarget = target.Center - projectile.Center;
                if (toTarget.LengthSquared() <= 1f) return;

                float dist = toTarget.Length();
                toTarget.Normalize();

                float desiredSpeed = MathHelper.Clamp(11f + dist / 65f, 13f, 26f);
                projectile.velocity = Vector2.Lerp(projectile.velocity, toTarget * desiredSpeed, 0.09f);
            }
            // 无目标时保持飞行惯性
            else if (projectile.velocity.LengthSquared() > 0.01f)
            {
                Vector2 dir = Vector2.Normalize(projectile.velocity);
                projectile.velocity = Vector2.Lerp(projectile.velocity, dir * 14f, 0.06f);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyHitNPC — 伤害修正（godMode 开启时生效）
        // ══════════════════════════════════════════════════════════════
        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;

            Player player = Main.player[projectile.owner];
            if (!player.active || !player.GetModPlayer<MyPlayer>().godModeBuff) return;

            // 🐉 星尘龙 ×8
            if (ProjectileID.Sets.StardustDragon[projectile.type])
                modifiers.SourceDamage *= 8f;
            // 🧠 星尘细胞本体 + 子细胞 ×15
            else if (projectile.type == ProjectileID.StardustCellMinion ||
                     projectile.type == ProjectileID.StardustCellMinionShot)
                modifiers.SourceDamage *= 15f;
            // ✨ 泰拉棱镜 ×13
            else if (projectile.type == ProjectileID.EmpressBlade)
                modifiers.SourceDamage *= 13f;
            // 🌙 月亮传送门激光 ×15
            else if (projectile.type == ProjectileID.MoonlordTurretLaser)
                modifiers.SourceDamage *= 15f;
            // 🌈 七彩水晶本体 + 爆炸 ×15
            else if (projectile.type == 643 || projectile.type == 644)
                modifiers.SourceDamage *= 15f;

            // 🪢 万花筒衰减（用独立 if，确保鞭子能同时应用其他倍率）
            if (ProjectileID.Sets.IsAWhip[projectile.type])
            {
                modifiers.SourceDamage *= currentWhipDecay;
                currentWhipDecay *= 0.95f;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   OnHitNPC — 命中后特效（godMode 开启时生效）
        // ══════════════════════════════════════════════════════════════
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;

            Player player = Main.player[projectile.owner];
            if (!player.active || !player.GetModPlayer<MyPlayer>().godModeBuff) return;

            // 🏹 幻影弓强化箭：链式跳跃 + 范围爆炸（绕过无敌帧）
            if (projectile.type == ModContent.ProjectileType<Projectiles.PhantasmSpecialArrowProj>())
                HandlePhantasmArrowHit(target, damageDone);

            // ☀️ 破晓之光矛：太阳爆发特效（每根矛只触发一次）
            if (projectile.type == 636 && !_daybreakBurstFiredSet.Contains(projectile.whoAmI))
            {
                _daybreakBurstFiredSet.Add(projectile.whoAmI);
                HandleDaybreakBurst(target, damageDone);
            }

            // 🐉 星尘龙命中：两轮链式溅射
            if (ProjectileID.Sets.StardustDragon[projectile.type])
            {
                HandleStardustDragonHit(target, damageDone);
                // 命中后设置3帧冷却
                _dragonTrackingCooldown = 3; 
            }

            // 🪢 鞭子命中：打暗印 + 重置衰减
            if (ProjectileID.Sets.IsAWhip[projectile.type])
            {
                target.AddBuff(ModContent.BuffType<DarkMarkBuff>(), 600);
                currentWhipDecay = 1f;
            }

            // 💀 召唤物命中带暗印的敌人：触发收割
            if ((projectile.minion ||
                 ProjectileID.Sets.MinionShot[projectile.type] ||
                 ProjectileID.Sets.SentryShot[projectile.type]) &&
                target.HasBuff(ModContent.BuffType<DarkMarkBuff>()))
            {
                DoHarvest(target, player);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   幻影弓命中效果：链式跳跃 + 范围爆炸 + 粒子特效
        // ══════════════════════════════════════════════════════════════
        private void HandlePhantasmArrowHit(NPC target, int damageDone)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // 链式跳跃：400f 内最近 3 个其他敌人，60% 伤害
                int chainCount = 0;
                for (int i = 0; i < Main.npc.Length; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, npc.Center) > 400f) continue;

                    npc.SimpleStrikeNPC((int)(damageDone * 0.6f), 0, false, 0, null, false, 0);
                    SpawnSplitVisual(target.Center, npc.Center);
                    if (++chainCount >= 3) break;
                }

                // 范围爆炸：300f 内其他敌人，40% 伤害
                for (int i = 0; i < Main.npc.Length; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, npc.Center) > 300f) continue;

                    npc.SimpleStrikeNPC((int)(damageDone * 0.4f), 0, false, 0, null, false, 0);
                }
            }

            // 爆炸粒子特效
            if (Main.netMode != NetmodeID.Server)
            {
                for (int i = 0; i < 12; i++)
                {
                    float angle = MathHelper.TwoPi / 12f * i;
                    Vector2 vel = new Vector2(
                        (float)Math.Cos(angle) * Main.rand.NextFloat(3f, 7f),
                        (float)Math.Sin(angle) * Main.rand.NextFloat(3f, 7f));
                    Dust.NewDustPerfect(target.Center, DustID.BlueFairy, vel, 0,
                        Color.Cyan, Main.rand.NextFloat(1f, 1.8f));
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   破晓之光太阳爆发：范围溅射 + 三层粒子特效
        // ══════════════════════════════════════════════════════════════
        private void HandleDaybreakBurst(NPC target, int damageDone)
        {
            // 范围溅射：300f 内其他敌人，50% 伤害
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < Main.npc.Length; i++)
                {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                    if (Vector2.Distance(target.Center, npc.Center) > 300f) continue;

                    npc.SimpleStrikeNPC((int)(damageDone * 0.5f), 0);
                }
            }

            if (Main.netMode == NetmodeID.Server) return;

            // 第一层：密集橙红火焰环
            for (int i = 0; i < 24; i++)
            {
                float angle = MathHelper.TwoPi / 24f * i;
                Vector2 vel = new Vector2(
                    (float)Math.Cos(angle) * Main.rand.NextFloat(5f, 10f),
                    (float)Math.Sin(angle) * Main.rand.NextFloat(5f, 10f));
                Dust.NewDustPerfect(target.Center, DustID.SolarFlare, vel, 0,
                    Color.OrangeRed, Main.rand.NextFloat(1.2f, 2f));
            }

            // 第二层：慢速金色光点
            for (int i = 0; i < 16; i++)
            {
                float angle = MathHelper.TwoPi / 16f * i;
                Vector2 vel = new Vector2(
                    (float)Math.Cos(angle) * Main.rand.NextFloat(2f, 5f),
                    (float)Math.Sin(angle) * Main.rand.NextFloat(2f, 5f));
                Dust.NewDustPerfect(target.Center, DustID.FireworkFountain_Yellow, vel, 0,
                    Color.Gold, Main.rand.NextFloat(1f, 1.6f));
            }

            // 中心爆炸白闪
            for (int i = 0; i < 8; i++)
            {
                Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.SolarFlare, Vector2.Zero, 0, Color.White, 2f);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   星尘龙命中效果：两轮链式溅射
        // ══════════════════════════════════════════════════════════════
        private void HandleStardustDragonHit(NPC target, int damageDone)
        {
            // 第一轮：500f 内最多 6 个敌人，80% 伤害
            int chainCount = 0;
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                if (Vector2.Distance(target.Center, npc.Center) > 500f) continue;

                npc.SimpleStrikeNPC((int)(damageDone * 0.8f), 0);
                SpawnSplitVisual(target.Center, npc.Center);
                if (++chainCount >= 6) break;
            }

            // 第二轮：300f 内所有敌人，50% 伤害
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.whoAmI == target.whoAmI) continue;
                if (Vector2.Distance(target.Center, npc.Center) >= 300f) continue;

                npc.SimpleStrikeNPC((int)(damageDone * 0.5f), 0);
                SpawnSplitVisual(target.Center, npc.Center);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyDamageHitbox — 鞭子判定框扩大
        // ══════════════════════════════════════════════════════════════
        public override void ModifyDamageHitbox(Projectile projectile, ref Rectangle hitbox)
        {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;

            Player player = Main.player[projectile.owner];
            if (!player.active || !player.GetModPlayer<MyPlayer>().godModeBuff) return;

            if (ProjectileID.Sets.IsAWhip[projectile.type])
                hitbox.Inflate(hitbox.Width / 2, hitbox.Height / 2);
        }

        // ══════════════════════════════════════════════════════════════
        //   OnKill — 清理破晓之光矛的触发记录，防止字典无限增长
        // ══════════════════════════════════════════════════════════════
        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (projectile.type == 636)
                _daybreakBurstFiredSet.Remove(projectile.whoAmI);
        }

        // ══════════════════════════════════════════════════════════════
        //   辅助方法
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 以 center 为圆心，在 maxRange 内找最近的可追踪 NPC
        /// </summary>
        private int AcquireNearestTarget(Vector2 center, float maxRange)
        {
            int bestIndex    = -1;
            float bestDistSq = maxRange * maxRange;

            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy()) continue;

                float distSq = Vector2.DistanceSquared(center, npc.Center);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex  = i;
                }
            }
            return bestIndex;
        }

        /// <summary>
        /// 收割：400f 内最多 6 个敌人各受 66 伤害，玩家获得 HarvestTimeBuff
        /// </summary>
        private void DoHarvest(NPC center, Player player)
        {
            int count = 0;
            for (int i = 0; i < Main.npc.Length; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly) continue;
                if (Vector2.Distance(center.Center, npc.Center) < 400f)
                {
                    npc.SimpleStrikeNPC(66, 50);
                    if (++count >= 6) break;
                }
            }
            player.AddBuff(ModContent.BuffType<HarvestTimeBuff>(), 180);
        }

        /// <summary>
        /// 链式溅射的青色电弧粒子线（仅客户端）
        /// </summary>
        private void SpawnSplitVisual(Vector2 from, Vector2 to)
        {
            if (Main.netMode == NetmodeID.Server) return;

            Dust.QuickDustLine(from, to, 10f, Color.Cyan);

            for (int i = 0; i < 6; i++)
            {
                Vector2 pos = Vector2.Lerp(from, to, i / 5f);
                Dust.NewDustPerfect(pos, DustID.Electric, Vector2.Zero, 0, Color.Cyan, 1.1f);
            }
            Dust.NewDustPerfect(to, DustID.Electric, Vector2.Zero, 0, Color.White, 1.4f);
        }
    }
}