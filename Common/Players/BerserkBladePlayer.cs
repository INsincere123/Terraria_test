using Terraria;
using Terraria.ModLoader;
using TestMod.Buffs;
using TestMod.Common.Systems;
using TestMod.Items.Accessories.Effects;
using TestMod.Items.DamageTypes;

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

        // 满层额外伤害配置（真实伤害，触发伤害的 17% + 固定 100，不暴击）
        private static readonly ExtraHitConfig MaxStackHitConfig = new()
        {
            FlatDamage          = BonusFlat,
            HitDamageRatio      = BonusHitRatio,
            StatType            = PlayerStatType.None,
            FollowWeapon        = false,
            FixedClass          = TrueDamageClass.Instance,
            UseCrit             = false,
            Knockback           = 0f,
            NoPlayerInteraction = false, // 同步网络包；_extraHitActive 防递归
        };

        // ── 帧状态 ──
        public  bool HasBerserkBlade;  // 本帧是否装备（每帧重置）
        public  int  Stacks;           // 当前叠层数（0~MaxStacks）
        public  int  Timer;            // 叠层倒计时，归零则清层

        private bool _extraHitActive;   // 防止满层额外一击递归触发
        private int  _projHitCounter;   // 近战弹幕命中计数（叠层/触发用）

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
                    _projHitCounter = 0;
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

        // ── 物品直接命中：仅真近战武器叠层 ─────────────────────────────────────
        // 灾厄加载时沿用 TrueMeleeDamageClass 判断；未加载时回退到 shoot == 0
        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!HasBerserkBlade || _extraHitActive) return;
            if (!CalamityCompatSystem.IsTrueMeleeWeapon(item)) return;

            TryStack(target, damageDone);
        }

        // ── 近战弹幕命中 ──────────────────────────────────────────────────────
        // 真近战武器产生的弹幕视为真近战，全效叠层
        // 其余近战弹幕效率降低：10次叠1层，满层后5次触发
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!HasBerserkBlade) return;

            // 真近战弹幕优先判断，不受外层 Melee 过滤影响
            if (CalamityCompatSystem.IsTrueMeleeProj(proj, Player.HeldItem))
            {
                if (_extraHitActive) return;
                TryStack(target, damageDone);
                return;
            }

            // 降效路径仅处理普通近战弹幕
            if (!proj.DamageType.CountsAsClass(DamageClass.Melee)) return;

            // 每次命中都刷新倒计时，防止叠层途中超时归零
            Timer = StackDuration;

            if (Stacks < MaxStacks)
            {
                // 未满层：每 ProjHitsPerStack 次命中叠 1 层
                _projHitCounter++;
                if (_projHitCounter >= ProjHitsPerStack)
                {
                    _projHitCounter = 0;
                    Stacks++;
                }
            }
            else
            {
                // 满层：每 ProjHitsPerTrigger 次命中触发一次额外伤害
                _projHitCounter++;
                if (_projHitCounter >= ProjHitsPerTrigger)
                {
                    _projHitCounter = 0;
                    _extraHitActive = true;
                    ExtraHitEffect.Strike(Player, target, MaxStackHitConfig, damageDone);
                    _extraHitActive = false;
                }
            }
        }

        // 叠层核心逻辑（两个钩子共用）
        private void TryStack(NPC target, int damageDone)
        {
            Stacks = System.Math.Min(Stacks + 1, MaxStacks);
            Timer  = StackDuration;

            if (Stacks == MaxStacks)
            {
                _extraHitActive = true;
                ExtraHitEffect.Strike(Player, target, MaxStackHitConfig, damageDone);
                _extraHitActive = false;
            }
        }
    }
}
