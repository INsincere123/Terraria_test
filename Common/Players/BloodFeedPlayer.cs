using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Buffs;
using TestMod.Common.DynamicText;

namespace TestMod.Common.Players
{
    // ============================================================================
    //  BloodFeedPlayer  ——  "生命资源化"模块核心
    // ----------------------------------------------------------------------------
    //  三阶段状态机（以 HasBuff 判断，tML 自动管理倒计时和显示）：
    //
    //  ① 正常阶段  HP > 20%：伤害/暴击随 HP 下降非线性增强（供武器读取）
    //  ② 猩红暴走  武器扣血后 HP ≤ 20%：通用加成爆发（5秒），每秒固定扣血
    //  ③ 战后虚弱  暴走结束后（10秒）：最大HP-20%，伤害在前4秒衰减至0
    //
    //  武器接入：
    //    Shoot()         → 扣血 + TriggerBerserkCheck()
    //    ModifyHitNPC()  → modifiers.SourceDamage *= CurrentNormalMultiplier
    // ============================================================================
    public class BloodFeedPlayer : ModPlayer
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        public const float BerserkThreshold       = 0.20f; // 暴走触发 HP 阈值
        public const int   BerserkDuration        = 300;   // 暴走持续帧数（5秒）
        public const int   ExhaustDuration        = 600;   // 虚弱持续帧数（10秒）
        public const int   DecayFrames            = 240;   // 虚弱期伤害加成衰减帧数（4秒）
        public const float MaxDamageBonus         = 9f;    // 最大附加伤害倍率（总计 ×10）
        public const float CurveExponent          = 2.5f;  // 伤害曲线指数（越大增长越后置）
        public const int   MaxCritBonus           = 50;    // 最大附加暴击率（+50%）
        public const float ExhaustHpRatio         = 0.20f; // 虚弱期最大HP减少比例（-20%）
        public const int   BerserkDrainInterval   = 60;    // 暴走扣血间隔（帧），60 = 1次/秒
        public const float BerserkDrainRatioPerTick = 0.02f; // 每次扣当前最大HP的比例（2%/秒）
        // ─────────────────────────────────────────────────────────────

        // 持久状态（不在 ResetEffects 里清零）
        public float BerserkPeakBonus;   // 暴走结束时的倍率，用于虚弱期衰减基准
        private bool _wasBerserkLastTick;
        private int  _drainAccum;        // 暴走扣血帧计数器

        // ── 只读属性 ──────────────────────────────────────────────────
        public bool  IsBerserk  => Player.HasBuff(ModContent.BuffType<CrimsonBerserkBuff>());
        public bool  IsExhausted => Player.HasBuff(ModContent.BuffType<BloodExhaustionBuff>());
        public float HpRatio    => Player.statLifeMax2 > 0
                                 ? (float)Player.statLife / Player.statLifeMax2
                                 : 1f;

        // 正常阶段倍率（武器自行读取并应用，暴走/虚弱的通用加成由本 ModPlayer 注入 Generic）
        public float CurrentNormalMultiplier => (IsBerserk || IsExhausted)
            ? 1f : ComputeDamageMultiplier(HpRatio);

        public int CurrentNormalCritBonus => (IsBerserk || IsExhausted)
            ? 0 : ComputeCritBonus(HpRatio);

        // ── 静态曲线（供武器直接调用）────────────────────────────────
        // f(loss) = 1 + 9 × loss^2.5   满HP→×1  40%HP→×3.5  20%HP→×6.15  0%→×10
        public static float ComputeDamageMultiplier(float hpRatio)
        {
            float loss = 1f - Math.Clamp(hpRatio, 0f, 1f);
            return 1f + MaxDamageBonus * MathF.Pow(loss, CurveExponent);
        }

        public static int ComputeCritBonus(float hpRatio)
        {
            float loss = 1f - Math.Clamp(hpRatio, 0f, 1f);
            return (int)(MaxCritBonus * MathF.Pow(loss, CurveExponent));
        }

        // ── ModPlayer 钩子 ────────────────────────────────────────────

        public override void PostUpdateEquips()
        {
            bool berserk  = IsBerserk;
            bool exhaust  = IsExhausted;

            // 检测暴走→虚弱的跳转（Buff 自然到期时触发）
            if (_wasBerserkLastTick && !berserk && !exhaust && !Player.dead)
            {
                BerserkPeakBonus = ComputeDamageMultiplier(HpRatio);
                Player.AddBuff(ModContent.BuffType<BloodExhaustionBuff>(), ExhaustDuration);
                exhaust = true;
            }
            _wasBerserkLastTick = berserk;

            if (berserk)
            {
                // 通用伤害/暴击加成（影响所有武器）
                float mult = ComputeDamageMultiplier(HpRatio);
                Player.GetDamage(DamageClass.Generic)    += mult - 1f;
                Player.GetCritChance(DamageClass.Generic) += ComputeCritBonus(HpRatio);

                // 每秒固定扣血（与攻击频率无关）
                _drainAccum++;
                if (_drainAccum >= BerserkDrainInterval)
                {
                    _drainAccum = 0;
                    int drain = Math.Max(1, (int)(Player.statLifeMax2 * BerserkDrainRatioPerTick));
                    drain = Math.Min(drain, Player.statLife - 1); // 保留最低1 HP
                    if (drain > 0)
                    {
                        Player.statLife -= drain;
                        Player.HealEffect(-drain, true); // 显示负数血量数字
                    }
                }
            }
            else if (exhaust)
            {
                _drainAccum = 0; // 进入虚弱后重置计数器

                // 衰减中的通用加成
                float decayMult = ComputeExhaustionMultiplier();
                if (decayMult > 1f)
                {
                    Player.GetDamage(DamageClass.Generic)    += decayMult - 1f;
                    float critFrac = (decayMult - 1f) / MaxDamageBonus;
                    Player.GetCritChance(DamageClass.Generic) += (int)(MaxCritBonus * critFrac);
                }
            }
            else
            {
                _drainAccum = 0;
            }
        }

        // 虚弱期最大HP减少（每帧重新应用，因为 statLifeMax2 每帧都被重算）
        public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
        {
            health = StatModifier.Default;
            mana   = StatModifier.Default;
            if (IsExhausted)
                health.Base -= (int)(Player.statLifeMax * ExhaustHpRatio);
        }

        // 虚弱期使用治疗药水：恢复量翻倍 + 清除所有 debuff（保留虚弱本身）
        public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
        {
            if (!IsExhausted || healValue <= 0) return;
            healValue *= 2;

            int exhaustType = ModContent.BuffType<BloodExhaustionBuff>();
            for (int i = Player.MaxBuffs - 1; i >= 0; i--)
            {
                int bt = Player.buffType[i];
                if (bt == 0 || bt == exhaustType) continue;
                if (Main.debuff[bt]) Player.DelBuff(i);
            }
        }

        // ── 内部方法 ──────────────────────────────────────────────────

        // 唯一的暴走触发入口，武器扣血后调用
        public void TriggerBerserkCheck()
        {
            if (!IsBerserk && !IsExhausted && HpRatio <= BerserkThreshold)
                StartBerserk();
        }

        private void StartBerserk()
        {
            // AddBuff 以真实帧数添加，tML 自动倒计时并显示
            Player.AddBuff(ModContent.BuffType<CrimsonBerserkBuff>(), BerserkDuration);
            DynamicWorldTextSystem.Spawn(new DynamicWorldTextRequest(
                "BLOOD FEED",
                Player.Top - Vector2.UnitY * 18f,
                DynamicTextStyleRegistry.BloodFeedBerserk,
                crit: true,
                scale: 0.72f,
                seed: Player.whoAmI * 409 + (int)Main.GameUpdateCount));
        }

        // 虚弱衰减：通过读取 buffTime 获取剩余时长，不再自维护计时器
        //   前 DecayFrames（4秒）= buffTime > ExhaustDuration - DecayFrames → 线性衰减
        //   后 6 秒 = buffTime ≤ ExhaustDuration - DecayFrames         → 固定 1.0f
        private float ComputeExhaustionMultiplier()
        {
            int idx = Player.FindBuffIndex(ModContent.BuffType<BloodExhaustionBuff>());
            if (idx < 0) return 1f;

            int timeLeft = Player.buffTime[idx];
            int decayEnd = ExhaustDuration - DecayFrames; // 360 帧

            if (timeLeft <= decayEnd) return 1f;

            // decayProgress: 0 = 刚进入虚弱，1 = 衰减完成
            float decayProgress = 1f - (float)(timeLeft - decayEnd) / DecayFrames;
            return MathHelper.Lerp(BerserkPeakBonus, 1f, decayProgress);
        }
    }
}
