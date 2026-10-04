using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Common.Compatibility;
using TestMod.Common.Utilities;
using TestMod.Common.Mechanics.AccessoryEffects;
using TestMod.Content.Items.DamageTypes;

namespace TestMod.Common.Players
{
    public class BerserkBladePlayer : ModPlayer
    {
        // ── 数值调节区 ──────────────────────────────────────────────────────────
        public  const int   MaxStacks      = 10;    // 最大叠层数
        private const int   StackDuration  = 120;   // 每层持续时间（帧），命中时重置
        private const float SpeedPerStack  = 0.04f; // 每层近战攻速加成（0.04 = 4%）
        private const float BonusFlat          = 100f;  // 满层额外伤害固定部分
        private const float BonusHitRatio      = 0.17f; // 满层额外伤害：触发伤害的百分比
        private const int   ProjHitsPerStack   = 10;     // 近战弹幕每 N 次命中叠 1 层
        private const int   ProjHitsPerTrigger = 5;     // 满层后近战弹幕每 N 次命中触发一次额外伤害
        // ────────────────────────────────────────────────────────────────────────

        // 固定 100 吃一次真实伤害完整增伤，实际命中伤害的 17% 不重复增伤；整次不暴击。
        private static readonly ExtraHitConfig MaxStackHitConfig = new()
        {
            FlatDamage          = BonusFlat,
            HitDamageRatio      = BonusHitRatio,
            StatType            = PlayerStatType.None,
            FollowWeapon        = false,
            FixedClass          = TrueDamageClass.Instance,
            UseCrit             = false,
            Knockback           = 0f,
            NoPlayerInteraction = false, // 保留玩家击杀归属；伤害包由统一入口同步
            CombatTextColor     = new Color(255, 255, 255),
            CombatTextStyleKey  = TestModTextStyles.DamageTrue,
        };

        // ── 帧状态 ──
        public  bool HasBerserkBlade;  // 本帧是否装备（每帧重置）
        public  int  Stacks;           // 当前叠层数（0~MaxStacks）
        public  int  Timer;            // 叠层倒计时，归零则清层

        private bool _extraHitActive;   // 防止满层额外一击递归触发
        private int  _projStackHits;    // 未满层时的普通近战弹幕命中进度
        private int  _projTriggerHits;  // 满层后独立累计，不继承叠层进度

        public override void ResetEffects()
        {
            HasBerserkBlade = false;
        }

        public override void PostUpdate()
        {
            if (Timer > 0)
            {
                Timer--;
                if (Timer == 0)
                {
                    Stacks = 0;
                    _projStackHits = 0;
                    _projTriggerHits = 0;
                }
            }

            // 有叠层时维持 buff 存活（keep-alive 模式）
            if (HasBerserkBlade && Stacks > 0)
                Player.AddBuff(ModContent.BuffType<BerserkBladeBuff>(), 5);
        }

        public override void PostUpdateEquips()
        {
            if (!HasBerserkBlade || Stacks <= 0) return;

            // 每层 +4% 近战攻速
            Player.GetAttackSpeed(DamageClass.Melee) += Stacks * SpeedPerStack;
        }

        // 进入此钩子就是物品本体命中；能够发射剑气不影响这次命中的快速资格。
        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!HasBerserkBlade || _extraHitActive) return;
            if (!item.DamageType.CountsAsClass(DamageClass.Melee)) return;

            TryStack(target, damageDone);
        }

        // ── 近战弹幕命中 ──────────────────────────────────────────────────────
        // 挥砍/刺击本体全效叠层，按弹幕自身分类，切武器不会改变旧弹幕待遇。
        // 其余近战弹幕效率降低：10次叠1层，满层后5次触发
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (ExtraHitEffect.IsExtraHitProjectile(proj)) return;
            if (!HasBerserkBlade || _extraHitActive) return;
            if (!proj.DamageType.CountsAsClass(DamageClass.Melee)) return;

            if (MeleeHitClassifier.IsDirectMelee(proj))
            {
                TryStack(target, damageDone);
                return;
            }

            // 每次命中都刷新倒计时，防止叠层途中超时归零
            Timer = StackDuration;

            if (Stacks < MaxStacks)
            {
                // 未满层：每 ProjHitsPerStack 次命中叠 1 层
                _projStackHits++;
                if (_projStackHits >= ProjHitsPerStack)
                {
                    _projStackHits = 0;
                    AddStack();
                }
            }
            else
            {
                // 满层：每 ProjHitsPerTrigger 次命中触发一次额外伤害
                _projTriggerHits++;
                if (_projTriggerHits >= ProjHitsPerTrigger)
                {
                    _projTriggerHits = 0;
                    StrikeExtraHit(target, damageDone);
                }
            }
        }

        // 叠层核心逻辑（两个钩子共用）
        private void TryStack(NPC target, int damageDone)
        {
            AddStack();
            Timer  = StackDuration;

            if (Stacks == MaxStacks)
            {
                StrikeExtraHit(target, damageDone);
            }
        }

        private void AddStack()
        {
            if (Stacks >= MaxStacks) return;
            Stacks++;
            if (Stacks == MaxStacks)
            {
                // 只在跨入满层时清零；已满层的快速命中不打断普通弹幕触发进度。
                _projStackHits = 0;
                _projTriggerHits = 0;
            }
        }

        private void StrikeExtraHit(NPC target, int damageDone)
        {
            _extraHitActive = true;
            try
            {
                ExtraHitEffect.Strike(Player, target, MaxStackHitConfig, damageDone);
            }
            finally
            {
                _extraHitActive = false;
            }
        }
    }
}
