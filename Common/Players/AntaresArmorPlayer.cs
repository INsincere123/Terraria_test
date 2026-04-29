using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using 武器test.Buffs;
using 武器test.Items.Armor;

namespace 武器test.Common.Players
{
    // 处理需要跨 tick 状态的盔甲效果：
    // 1. 百分比生命/法力加成（需要在 PostUpdateMiscEffects 读取最终基础值）
    // 2. 套装复活效果（PreKill 拦截死亡，CD 用 AntaresReviveCooldown debuff 计时）
    // 3. 套装秒杀免疫（ModifyHitByNPC / ModifyHitByProjectile，原始攻击力 >= 最大生命值时强制为 1，无 CD）
    // 4. 套装主动技能：引力井（按键施加增益 buff，buff 存在期间每帧持续拉取）
    public class AntaresArmorPlayer : ModPlayer
    {
        // ╔══════════════════════════════════════════════════════╗
        // ║           引力井主动技能参数调整区域                 ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  GravityWellDuration   每次按键的持续时间（帧）      ║
        // ║  GravityWellRadius     拉取范围（像素，16px = 1格）  ║
        // ║  GravityWellStrength   拉取速度（像素/帧）           ║
        // ║  GravityWellMaxTargets 每帧最多处理的敌人数量        ║
        // ║  GravityWellStopDist   拉取后与玩家的最小距离（像素）║
        // ╚══════════════════════════════════════════════════════╝

        public const int GravityWellDuration = 60 * 2;    // 2 秒持续时间
        public const float GravityWellRadius = 16f * 50;  // 50 格范围
        public const float GravityWellStrength = 18f;       // 拉取速度（像素/帧）
        public const int GravityWellMaxTargets = 20;        // 每帧最多处理 20 个敌人
        public const float GravityWellStopDist = 16f * 7;   // 停在玩家 7 格外

        // ══════════════════════════════════════════════════════

        // ── 穿戴标记 ─────────────────────────────────────────
        public bool wearingHelmet = false;
        public bool wearingBreastplate = false;
        public bool wearingLeggings = false;
        public bool wearingFullSet = false;

        public override void ResetEffects()
        {
            wearingHelmet = false;
            wearingBreastplate = false;
            wearingLeggings = false;
            wearingFullSet = false;
        }

        // ── 百分比生命/法力加成 ───────────────────────────────
        public override void PostUpdateMiscEffects()
        {
            if (wearingHelmet)
            {
                Player.statLifeMax2 += (int)(Player.statLifeMax * AntaresHelmet.MaxLifeBonus);
                Player.statManaMax2 += (int)(Player.statManaMax * AntaresHelmet.MaxManaBonus);
            }
            if (wearingBreastplate)
            {
                Player.statLifeMax2 += (int)(Player.statLifeMax * AntaresBreastplate.MaxLifeBonus);
                Player.statManaMax2 += (int)(Player.statManaMax * AntaresBreastplate.MaxManaBonus);
            }
            if (wearingLeggings)
            {
                Player.statLifeMax2 += (int)(Player.statLifeMax * AntaresLeggings.MaxLifeBonus);
                Player.statManaMax2 += (int)(Player.statManaMax * AntaresLeggings.MaxManaBonus);
            }
        }

        // ── 引力井主动技能 ────────────────────────────────────
        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer || !wearingFullSet)
                return;

            // 按键触发：施加或刷新增益 buff
            if (AntaresKeybinds.GravityWellKey.JustPressed)
            {
                // AddBuff 对已存在的 buff 会刷新为新的持续时间
                Player.AddBuff(ModContent.BuffType<AntaresGravityWellBuff>(), GravityWellDuration);

                // 按键时播放音效和粒子（只在触发/刷新时触发一次）
                SoundEngine.PlaySound(SoundID.Item9, Player.Center);
                SpawnGravityWaveParticles();
            }

            // buff 存在期间每帧持续拉取
            if (Player.HasBuff(ModContent.BuffType<AntaresGravityWellBuff>()))
                PullNearbyEnemies();
        }

        private void PullNearbyEnemies()
        {
            int pulled = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (pulled >= GravityWellMaxTargets)
                    break;

                NPC npc = Main.npc[i];

                // 跳过无效、友好、Boss、不可被击中的敌人
                if (!npc.active || npc.friendly || npc.boss || npc.dontTakeDamage)
                    continue;

                float dist = Vector2.Distance(Player.Center, npc.Center);

                if (dist > GravityWellRadius)
                    continue;

                if (dist <= GravityWellStopDist)
                    continue;

                Vector2 direction = Player.Center - npc.Center;
                direction.Normalize();

                Vector2 targetPos = Player.Center - direction * GravityWellStopDist;
                Vector2 toTarget = targetPos - npc.Center;
                float travelDist = toTarget.Length();

                if (travelDist < 1f)
                    continue;

                int frames = Math.Max(1, (int)(travelDist / GravityWellStrength));
                npc.velocity = toTarget / frames;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);

                pulled++;
            }
        }

        private void SpawnGravityWaveParticles()
        {
            int ringCount = 2;
            int particlesPerRing = 24;

            for (int ring = 0; ring < ringCount; ring++)
            {
                float ringRadius = GravityWellRadius * (0.3f + ring * 0.35f);

                for (int j = 0; j < particlesPerRing; j++)
                {
                    float angle = MathHelper.TwoPi / particlesPerRing * j;
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * ringRadius;
                    Vector2 spawnPos = Player.Center + offset;

                    Vector2 vel = offset;
                    vel.Normalize();
                    vel *= 3f + ring * 1.5f;

                    int d = Dust.NewDust(spawnPos, 0, 0, DustID.RedTorch, vel.X, vel.Y, 120, default, 1.8f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].fadeIn = 0.5f;
                }
            }

            for (int j = 0; j < 16; j++)
            {
                float angle = MathHelper.TwoPi / 16 * j;
                float spawnRadius = GravityWellRadius * 0.6f;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * spawnRadius;
                Vector2 spawnPos = Player.Center + offset;

                Vector2 vel = -offset;
                vel.Normalize();
                vel *= 5f;

                int d = Dust.NewDust(spawnPos, 0, 0, DustID.CrimsonTorch, vel.X, vel.Y, 150, default, 1.2f);
                Main.dust[d].noGravity = true;
            }
        }

        // ── 秒杀免疫：NPC 近战攻击 ───────────────────────────
        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers)
        {
            if (!wearingFullSet)
                return;

            TryBlockOneshot(ref modifiers, npc.damage);
        }

        // ── 秒杀免疫：弹幕攻击 ───────────────────────────────
        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers)
        {
            if (!wearingFullSet)
                return;

            TryBlockOneshot(ref modifiers, proj.damage);
        }

        // ── 秒杀免疫核心逻辑 ─────────────────────────────────
        // rawDamage 为攻击方原始攻击力，在防御减算之前判断
        // 原始攻击力 >= 最大生命值时将伤害变为 1，并给予短暂无敌，CD 由游戏内的免疫时间控制，无需额外计时
        private void TryBlockOneshot(ref Player.HurtModifiers modifiers, int rawDamage)
        {
            if (rawDamage < Player.statLifeMax2)
                return;

            // 伤害归零
            modifiers.FinalDamage *= 0f;

            // 短暂无敌帧防止同帧连续判定
            Player.immune = true;
            Player.immuneTime = 60;
            Player.hurtCooldowns[0] = 60;
            Player.hurtCooldowns[1] = 60;

            // 金色粒子视觉反馈
            for (int i = 0; i < 20; i++)
            {
                int d = Dust.NewDust(Player.position, Player.width, Player.height,
                    DustID.GoldFlame, 0f, 0f, 0, default, 1.5f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 3f;
            }
        }

        // ── 套装复活核心逻辑 ──────────────────────────────────
        public override bool PreKill(double damage, int hitDirection, bool pvp,
            ref bool playSound, ref bool genGore,
            ref Terraria.DataStructures.PlayerDeathReason damageSource)
        {
            if (!wearingFullSet)
                return true;

            if (Player.HasBuff(ModContent.BuffType<AntaresReviveCooldown>()))
                return true;

            Player.AddBuff(ModContent.BuffType<AntaresReviveCooldown>(), AntaresHelmet.ReviveCooldown);

            int healAmount = (int)(Player.statLifeMax2 * AntaresHelmet.ReviveLifePercent);
            Player.statLife = healAmount;

            Player.immune = true;
            Player.immuneTime = 60 * 3; // 复活后的无敌时间

            Player.HealEffect(healAmount);

            for (int i = 0; i < 30; i++)
            {
                int d = Dust.NewDust(Player.position, Player.width, Player.height,
                    DustID.FireworkFountain_Red, 0f, 0f, 0, default, 2f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 4f;
            }

            return false;
        }
    }
}
