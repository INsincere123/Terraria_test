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
    public partial class MyGlobalProjectile : GlobalProjectile
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
        // 破晓之光追踪延迟计时器
        private int _daybreakTrackDelay = 0;

        // ══════════════════════════════════════════════════════════════
        //   PostAI — 每帧追踪入口（按弹射物类型分发到各专属追踪逻辑）
        // ══════════════════════════════════════════════════════════════
        public override void PostAI(Projectile projectile)
        {

            //Main.NewText($"PostAI type={projectile.type}");  // 临时探针

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
            // 🌌 星云烈焰普通弹(3541) + Ex弹(3542)：环射 + 追踪扩大到 800px
            else if (projectile.type == ProjectileID.NebulaBlaze1 || projectile.type == ProjectileID.NebulaBlaze2)
            {
                if (!godMode) return;
                TrySpawnNebulaBlazeRing(projectile);
                ApplyNebulaBlazeBoostedTracking(projectile);
            }
            // ☀️ 破晓之光矛（仅飞行中追踪，插入敌人后不再干预）
            else if (projectile.type == 636)
            {
                if (!godMode || projectile.ai[0] != 0) return;
                ApplyDaybreakTracking(projectile);
            }
            // 👻 普通召唤物（排除哨兵、模组和星尘细胞本体）
            else if (projectile.minion && !projectile.sentry &&
                     projectile.type != ProjectileID.StardustCellMinion &&
                     projectile.type < ProjectileID.Count)  // 排除模组召唤物
            {
                ApplyGenericMinionTracking(projectile, player, summonRange);
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
    }
}
