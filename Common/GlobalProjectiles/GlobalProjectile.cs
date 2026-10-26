using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using TestMod.Common.DataStructures;
using TestMod.Common.Compatibility;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Common.Mechanics.AccessoryEffects;
using TestMod.Content.Items.DamageTypes;
using TestMod.Content.Projectiles.Melee;
using TestMod.Content.Projectiles.Minions;
using TestMod.Content.Projectiles.Accessories;

namespace TestMod.Common.GlobalProjectiles
{
    /// <summary>
    /// 全局弹射物钩子：武器强化的核心处理类
    /// 包含追踪增强、伤害修正、命中特效三大功能
    /// </summary>
    public partial class GlobalProjectile : Terraria.ModLoader.GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // Subhand 自己发出的原版 MagnetSphereBolt 用这个标记接管伤害数字，避免影响原版磁球武器。
        public bool subhandDynamicText;

        // 额外伤害弹幕的结算规则（每实体保存，随 ExtraAI 同步）。
        public bool IsExtraHit;
        public bool ExtraHitUseCrit;
        public bool ExtraHitIgnoreDefense;

        // ── 星尘龙追踪冷却 ──
        private int _dragonTrackingCooldown = 0;

        // 破晓之光追踪延迟计时器
        private int _daybreakTrackDelay = 0;

        // ── 减速力场：保存/恢复速度，防止衰减 ──
        // AI 每帧可能只改方向不改速度大小，若每帧乘 factor 会指数衰减。
        // 做法：PreAI 恢复上帧保存的"未缩放速度"→ AI 正常跑 → PostAI 保存并缩放。
        internal Microsoft.Xna.Framework.Vector2 _sfNaturalVelocity; // AI 跑完后的自然速度
        internal bool _sfInField;


        // ══════════════════════════════════════════════════════════════
        //   PreAI — 乌鸦专属：完全接管 vanilla AI（返回 false 跳过原版 AI）
        //
        //   vanilla Raven 的 AI 状态机每帧会操作 friendly/damage 等属性，
        //   即使 PostAI 覆盖 velocity 也无法解决"撞到 boss 没伤害"问题，
        //   因此必须用 PreAI 彻底屏蔽 vanilla AI，完全由我们驱动行为。
        // ══════════════════════════════════════════════════════════════
        public override bool PreAI(Projectile projectile)
        {
            GrappleEffect.UpdateLaunchSpeed(projectile, ref _grappleLaunchBoostApplied, !IsTimeEchoAttack);
            RangedMotion_RestoreVelocity(projectile);
            Seals_UpdateRocketExtraUpdates(projectile);
            ProjectileLookup.Observe(projectile, this);
            ResetTrackingUpdate();
            _dragonFlightPrepared = false;
            // 减速力场：在 AI 跑之前恢复上帧保存的自然速度，防止衰减
            SlowField_RestoreVelocity(projectile);
            if (IsTimeEchoAttack) return TimeEcho_PreAI(projectile);
            UpdateContactTrackingImmunity(projectile);

            // 暴走期间：在碰撞检测前强制弹幕无限穿透
            BloodFeed_SetBerserkPenetrate(projectile);

            // ⏱ 时停拦截：在所有 AI 处理之前。
            // 仅冻结敌方弹幕（hostile && !friendly），玩家弹幕完全不受影响。
            if (TryFreezeOnTimeStop(projectile))
                return false;

            PrepareStardustDragonFlight(projectile);

            if (projectile.type == ProjectileID.EmpressBlade && HasEnhancedTerraprisma)
            {
                if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return false;
                Player owner = Main.player[projectile.owner];
                if (!owner.active) return false;
                ApplyTerraprismaAI(projectile, owner);
                return false;
            }

            if (projectile.type == ProjectileID.AbigailMinion && HasTrackingWeapon(TrackingWeaponKind.Abigail))
            {
                if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return false;
                Player owner = Main.player[projectile.owner];
                if (!owner.active) return false;
                ApplyAbigailAI(projectile, owner);
                return false;
            }

            if (projectile.type != ProjectileID.Raven || !HasTrackingWeapon(TrackingWeaponKind.Raven)) return true;
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return true;

            Player player = Main.player[projectile.owner];
            if (!player.active) return true;

            // 强制攻击属性，防止任何残留逻辑禁用伤害判定
            projectile.friendly = true;
            projectile.hostile = false;

            // vanilla AI 被跳过 → 手动递减 localNPCImmunity
            // （原本这段递减逻辑在 Projectile.AI() 内部，不做会死锁）
            if (projectile.usesLocalNPCImmunity && projectile.localNPCImmunity != null)
            {
                for (int i = 0; i < projectile.localNPCImmunity.Length; i++)
                {
                    if (projectile.localNPCImmunity[i] > 0)
                        projectile.localNPCImmunity[i]--;
                }
            }

            ApplyCorvidRavenAI(projectile, player);
            return false; // 跳过 vanilla AI
        }

        public override void SetDefaults(Projectile projectile)
        {
            _grappleLaunchBoostApplied = false;
            PlusWeapon = PlusWeaponKind.None;
            ExcludedFromGodSmith = false;
            DecoySource = default;
            DecoyExcluded = false;
            DecoySelection.Reset();
            TrackingWeapon = TrackingWeaponKind.None;
            _minionAttackSource = false;
            _trackingDefaultsApplied = _contactImmunityApplied = false;
        }

        // ══════════════════════════════════════════════════════════════
        //   PostAI — 每帧追踪入口（按弹射物类型分发到各专属追踪逻辑）
        // ══════════════════════════════════════════════════════════════
        public override void PostAI(Projectile projectile)
        {
            GrappleEffect.UpdateLaunchSpeed(projectile, ref _grappleLaunchBoostApplied, !IsTimeEchoAttack);
            RangedMotion_ApplyVelocity(projectile);
            if (IsTimeEchoAttack)
            {
                TimeEcho_PostAI(projectile);
                if (!projectile.active) return;
                ApplyTimeSlowToProjectile(projectile);
                RequestTrackingSync(projectile);
                return;
            }
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;

            Player player = Main.player[projectile.owner];
            if (!player.active) return;

            // ─────────────── 专属处理分支 (优先级最高) ───────────────

            // 排除名单优先于所有专属和通用追踪，仍保留末尾公共处理。
            if (VanillaMinionExclusions.Contains(projectile.type))
            {
                // 保持原版本体 AI；强化武器的伤害由来源标记独立处理。
            }
            else if (projectile.type == ProjectileID.AbigailMinion || projectile.type == ProjectileID.EmpressBlade)
            {
                // PreAI 已完整处理攻击运动，保留末尾公共处理与同步。
            }
            // 🐉 星尘龙（仅龙头；通用追踪与强化武器的伤害分别处理）
            else if (ProjectileID.Sets.StardustDragon[projectile.type])
            {
                if (projectile.type == ProjectileID.StardustDragon1 && HasEnhancedDragon)
                    ApplyStardustDragonTracking(projectile);
            }
            // 🐦‍⬛ 乌鸦(317) 由 PreAI 接管，此处不重复处理
            // 🐯 沙漠虎三形态(833=幼崽 / 834=成年 / 835=装甲)：龙头级别追踪
            else if (projectile.type == ProjectileID.StormTigerTier1 ||
                     projectile.type == ProjectileID.StormTigerTier2 ||
                     projectile.type == ProjectileID.StormTigerTier3)
            {
                if (HasTrackingWeapon(TrackingWeaponKind.DesertTiger)) ApplyLegacyTigerTracking(projectile);
            }
            // 🌌 星云烈焰普通弹 + Ex弹：环射 + 2400px 通用追踪范围
            else if (projectile.type == ProjectileID.NebulaBlaze1 || projectile.type == ProjectileID.NebulaBlaze2)
            {
                if (HasPlusWeapon(PlusWeaponKind.NebulaBlaze))
                {
                    TrySpawnNebulaBlazeRing(projectile);
                    ApplyNebulaBlazeBoostedTracking(projectile);
                }
            }
            // ☀️ 破晓之光矛（仅飞行中追踪，插入敌人后不再干预）
            else if (projectile.type == ProjectileID.Daybreak)
            {
                if (HasPlusWeapon(PlusWeaponKind.Daybreak) && projectile.ai[0] == 0)
                    ApplyDaybreakTracking(projectile);
            }
            // 🕷️ 蜘蛛法杖：独立无敌帧 + 扩展瞄准/返回范围
            else if (IsSpiderMinion(projectile.type))
            {
                if (HasTrackingWeapon(TrackingWeaponKind.Spider)) ApplySpiderPostAI(projectile, player);
            }

            // ─────────────── 通用分类分支 ───────────────

            // 专用 AI 自行管理停追冷却、起飞延迟和指定目标，不再叠加通用追踪。
            else if (projectile.ModProjectile is AntaresBeam or QuasarJetBurstShard)
            {
                // 保留后续时缓与显式强制追踪的处理入口。
            }
            // 🔫 ③ 召唤物射弹：标准标记兜底，真实仆从及派生来源补足模组未登记的射弹。
            //    覆盖星尘细胞子弹/大黄蜂尖刺/UFO激光/双子激光/迷你鲨鱼等
            else if (ProjectileID.Sets.MinionShot[projectile.type] || IsSpiritGuidanceSourceShot(projectile))
            {
                if (HasPlusWeapon(PlusWeaponKind.StardustCell))
                    ApplyHighTierTracking(projectile, 16.1f, 69f, 0.10f, 0.225f, prioritizeMinionTarget: true);
                else if (HasSpiritGuidanceForShot(player, projectile))
                    ApplyHighTierTracking(projectile, 13f, 55f, 0.08f, 0.18f, prioritizeMinionTarget: true, range: 1300f);
            }
            // 🚫 ② 射击型本体：保持 vanilla AI，不干预
            //    (贴敌会让它们不发射射弹,必须放弃追踪强化)
            else if (IsShootingMinionBody(projectile.type))
            {
                // 故意留空 —— 完全交给 vanilla AI
            }
            // 🥊 ① 冲撞型召唤物：蜘蛛式扩大范围追踪
            else if (IsContactMinion(projectile))
            {
                if (HasSpiritGuidance(player)) ApplyContactMinionTracking(projectile, player);
            }

            // 万花筒范围扩大已移至 CorePlayer.PostUpdateEquips，通过 whipRangeMultiplier 实现

            // 时缓：PostAI 末尾缩放速度（AI 跑完后再缩，防止被覆盖）
            ApplyTimeSlowToProjectile(projectile);

            // 强制追踪：最后应用，覆盖其他速度修改
            ForcedHoming_Update(projectile);
            RequestTrackingSync(projectile);
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyHitNPC — 通用伤害规则与独立强化武器
        // ══════════════════════════════════════════════════════════════
        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;

            Player player = Main.player[projectile.owner];
            if (projectile.ModProjectile is InstantExtraHitProjectile) return;

            // 真实伤害：无视防御（不依赖强化武器）；通用减伤由 TrueDamageSystem 在最终计算时处理。
            if (projectile.DamageType == TrueDamageClass.Instance)
            {
                modifiers.ScalingArmorPenetration += 1f;
                modifiers.HideCombatText();
            }

            if (subhandDynamicText)
                modifiers.HideCombatText();

            if (IsTimeEchoAttack)
            {
                TimeEcho_ModifyHit(projectile, ref modifiers);
                return;
            }

            if (IsExtraHit)
            {
                ExtraHitEffect.ApplyHitRules(player, projectile.DamageType, ExtraHitUseCrit, ExtraHitIgnoreDefense, ref modifiers);
                return;
            }

            // 暴走期间：弹幕无视防御
            if (player.active && player.GetModPlayer<BloodFeedPlayer>().IsBerserk)
                modifiers.ScalingArmorPenetration += 1f;

            // 鞭子 tag 效果：不依赖强化武器
            WhipTag_ModifyHitNPC(projectile, target, ref modifiers);

        }

        // ══════════════════════════════════════════════════════════════
        //   OnHitNPC — 命中后特效
        //
        //   · 冲撞型召唤物的"弹开"是 QoL 功能,不依赖强化武器
        // ══════════════════════════════════════════════════════════════
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;

            Player player = Main.player[projectile.owner];
            if (projectile.ModProjectile is InstantExtraHitProjectile) return;
            if (!player.active) return;

            if (projectile.DamageType == TrueDamageClass.Instance && projectile.owner == Main.myPlayer)
            {
                TextRenderingBridge.SpawnCombatText(
                    target.Hitbox,
                    damageDone,
                    hit.Crit,
                    TextRenderingBridge.GetStyleColor(TestModTextStyles.DamageTrue),
                    TestModTextStyles.DamageTrue);
            }
            else if (subhandDynamicText && projectile.owner == Main.myPlayer)
            {
                TextRenderingBridge.SpawnCombatText(
                    target.Hitbox,
                    damageDone,
                    hit.Crit,
                    TextRenderingBridge.GetStyleColor(TestModTextStyles.Subhand),
                    TestModTextStyles.Subhand);
            }

            // 额外伤害只完成投送和数字显示，不再次触发追加攻击或鞭爆炸。
            if (IsExtraHit || IsTimeEchoAttack) return;

            VortexQuiver_OnHitNPC(projectile, target);

            // 通过 GlobalProjectile 路径分发副手等追加攻击效果
            // 不走 ModPlayer.OnHitNPCWithProj 是因为自定义 DamageClass 对该钩子存在兼容性问题
            if (projectile.friendly && !projectile.hostile)
                player.GetModPlayer<OnHitEffectsPlayer>().DispatchProjectileHit(projectile, target, hit, damageDone);

            // 冲撞型由御灵核心启用，蜘蛛仅由对应强化武器启用。
            //    防止贴在 boss 身上空转无敌帧
            if ((IsContactMinion(projectile) && HasSpiritGuidance(player)) ||
                (IsSpiderMinion(projectile.type) && HasTrackingWeapon(TrackingWeaponKind.Spider)))
            {
                ApplyContactMinionBounce(projectile, target);
            }

            // 🪢 鞭子 tag 效果：不依赖强化武器
            WhipTag_OnHitNPC(projectile, target, hit, damageDone);

            // 仅强化星尘龙使用命中后停追。
            if (HasEnhancedDragon && ProjectileID.Sets.StardustDragon[projectile.type])
                _dragonTrackingCooldown = 3;
        }
    }
}
