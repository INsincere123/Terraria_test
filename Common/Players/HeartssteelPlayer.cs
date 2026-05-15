using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TestMod.Common.Players
{
    // ============================================================================
    //  HeartssteelPlayer  ——  心之钢饰品的全部运行时状态与逻辑
    // ----------------------------------------------------------------------------
    //  持久化字段:
    //   BonusMaxHP   — 永久叠层的最大生命（存档保存，卸下装备后保留但不生效）
    //
    //  帧状态:
    //   ChargeTimer  — 当前蓄力计时（Boss 在附近时每帧 +1）
    //   IsCharged    — 是否已充能，等待下一次命中 Boss 触发
    //   HasHeartsteel— 本帧是否装备了心之钢（ResetEffects 重置，装备时置 true）
    // ============================================================================
    public class HeartssteelPlayer : ModPlayer
    {
        // ── 数值调节区 ──────────────────────────────────────────────────────────
        private const int   ChargeTicks      = 120;    // 蓄力时间：2 秒（60 帧/秒）
        private const float DetectRange      = 960f;   // Boss 检测范围：60 格（1 格 = 16 px）
        private const float BonusDmgFlat     = 240f;   // 触发额外伤害固定部分
        private const float BonusDmgHpRatio  = 0.88f;  // 触发额外伤害：基于最大生命的比例
        private const float HpGainRatio      = 0.008f; // 每次触发：从实际伤害中获得的永久生命比例
        public  const int   MaxBonusHP       = 200;    // 永久叠层生命上限
        // ────────────────────────────────────────────────────────────────────────

        // ── 持久状态（跨存档保存）──
        public int BonusMaxHP;

        // ── 帧状态 ──
        public int  ChargeTimer;
        public bool IsCharged;
        public bool HasHeartsteel;

        // 防止 SimpleStrikeNPC 触发 OnHitNPC 时递归进入 TryTriggerProc
        private bool _procActive;

        public override void ResetEffects()
        {
            HasHeartsteel = false;
        }

        public override void PostUpdate()
        {
            if (!HasHeartsteel) return;

            // 检测附近 Boss（用距离平方避免 sqrt 开销）
            float rangeSquared = DetectRange * DetectRange;
            bool bossNearby = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.boss) continue;
                if (Vector2.DistanceSquared(Player.Center, npc.Center) <= rangeSquared)
                {
                    bossNearby = true;
                    break;
                }
            }

            // Boss 在附近时持续蓄力；离开后保留已有进度和充能状态
            if (bossNearby && !IsCharged)
            {
                ChargeTimer++;
                if (ChargeTimer >= ChargeTicks)
                    IsCharged = true;
            }

            // 充能就绪时的视觉提示（金色火焰粒子）
            if (IsCharged && Main.rand.NextBool(8))
            {
                Dust.NewDust(
                    Player.position, Player.width, Player.height,
                    DustID.GoldFlame,
                    Scale: Main.rand.NextFloat(0.8f, 1.6f));
            }
        }

        // 装备时每帧将叠层生命加入最大生命上限（卸下后不生效，叠层数保留）
        public override void PostUpdateMiscEffects()
        {
            if (HasHeartsteel && BonusMaxHP > 0)
                Player.statLifeMax2 += BonusMaxHP;
        }

        // 物品直接命中
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            TryTriggerProc(target);
        }

        // 弹射物命中
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            TryTriggerProc(target);
        }

        private void TryTriggerProc(NPC target)
        {
            // _procActive 防止 SimpleStrikeNPC(noPlayerInteraction:false) 触发的 OnHitNPC 递归进来
            if (_procActive || !HasHeartsteel || !IsCharged) return;
            if (!target.active || !target.boss || target.life <= 0) return;

            _procActive   = true;
            IsCharged     = false;
            ChargeTimer   = 0;

            // 根据当前持握武器决定伤害类型（非武器/空手默认近战）
            Item heldItem = Player.HeldItem;
            DamageClass dmgClass = (heldItem.IsAir || heldItem.damage <= 0)
                ? DamageClass.Melee
                : (heldItem.DamageType ?? DamageClass.Melee);

            // 基础伤害 × 玩家当前伤害加成
            int baseDamage    = (int)(BonusDmgFlat + Player.statLifeMax2 * BonusDmgHpRatio);
            int scaledDamage  = (int)Player.GetDamage(dmgClass).ApplyTo(baseDamage);

            // 使用玩家暴击率做独立判定
            bool crit      = Player.GetCritChance(dmgClass) > Main.rand.NextFloat() * 100f;
            int  direction = target.Center.X > Player.Center.X ? 1 : -1;

            // 独立一击：走 NPC 防御计算，触发 OnHit 系列钩子（含其它 mod 效果）
            // noPlayerInteraction:false → 多人模式下自动同步伤害包
            int damageDone = target.SimpleStrikeNPC(
                scaledDamage, direction,
                crit:               crit,
                knockBack:          0f,
                damageType:         dmgClass,
                noPlayerInteraction: false);

            // 永久叠层：基于实际打出的伤害（已扣除 NPC 防御）
            if (damageDone > 0 && BonusMaxHP < MaxBonusHP)
            {
                int gainedHP = Math.Max(1, (int)(damageDone * HpGainRatio));
                BonusMaxHP   = Math.Min(MaxBonusHP, BonusMaxHP + gainedHP);
            }

            SpawnProcEffect(target);

            _procActive = false;
        }

        private void SpawnProcEffect(NPC target)
        {
            for (int i = 0; i < 30; i++)
            {
                Dust dust = Dust.NewDustDirect(
                    target.position, target.width, target.height,
                    DustID.GoldFlame,
                    Main.rand.NextFloat(-5f, 5f),
                    Main.rand.NextFloat(-5f, 5f),
                    Scale: Main.rand.NextFloat(1.2f, 2.4f));
                dust.noGravity = true;
            }
        }

        public override void SaveData(TagCompound tag)
        {
            tag["BonusMaxHP"] = BonusMaxHP;
        }

        public override void LoadData(TagCompound tag)
        {
            BonusMaxHP = tag.GetInt("BonusMaxHP");
        }
    }
}
