using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.AccessoryEffects;
using TestMod.Content.Buffs;

namespace TestMod.Common.Players
{
    public class AdversityAmuletPlayer : ModPlayer
    {
        // ── 数值调节区：恢复以 HP/秒计，持续时间以游戏 tick 计 ──
        public const int MaxStacks = 5;
        public const int StackDuration = 11 * 60;
        public const float ReductionPerStack = 0.15f;
        private const int LifeRegenPerSecond = 4;
        private const int DefenseBonus = 18;
        private const int MaxLifeBonus = 60;
        private const float ContactReduction = 0.11f;

        private enum AttackKind : byte { None, NpcContact, Projectile }

        public bool HasAdversityAmulet;
        public int Stacks { get; private set; }
        public int RemainingFrames { get; private set; }
        public int RemainingSeconds => (RemainingFrames + 59) / 60;
        public float AdaptationReduction => Stacks * ReductionPerStack;

        private AttackKind _attackKind;
        private int _attackType;

        public override void ResetEffects() => HasAdversityAmulet = false;

        public override void PreUpdate()
        {
            // 在本帧装备结算前到期，避免多保留一帧翻倍属性。
            if (RemainingFrames > 0 && --RemainingFrames == 0)
                ClearAdaptation();
        }

        public override void PostUpdateEquips()
        {
            if (!HasAdversityAmulet || Player.dead)
            {
                ClearAdaptation();
                return;
            }

            int multiplier = Stacks > 0 ? 2 : 1;
            DefensiveStatsEffect.Apply(Player, new DefensiveStatsConfig
            {
                Defense = DefenseBonus * multiplier,
                LifeRegenPerSec = LifeRegenPerSecond * multiplier
            });
            Player.statLifeMax2 += MaxLifeBonus * multiplier;
        }

        public override void PostUpdate()
        {
            if (!HasAdversityAmulet || Player.dead)
                ClearAdaptation();
            else if (Stacks > 0)
                UpdateDisplayBuff();
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers)
        {
            if (!HasAdversityAmulet) return;

            // 接触减伤与适应减伤分别相乘，不混入通用 endurance。
            modifiers.FinalDamage *= 1f - ContactReduction * (Stacks > 0 ? 2 : 1);
            ApplyAdaptation(AttackKind.NpcContact, npc.type, ref modifiers);
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers)
        {
            if (HasAdversityAmulet)
                ApplyAdaptation(AttackKind.Projectile, proj.type, ref modifiers);
        }

        private void ApplyAdaptation(AttackKind kind, int type, ref Player.HurtModifiers modifiers)
        {
            if (Stacks > 0 && _attackKind == kind && _attackType == type)
                modifiers.FinalDamage *= 1f - AdaptationReduction;
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            // OnHurt 已在伤害结算之后；取消/闪避/护盾完全吸收不会进入此处。
            // 联机伤害包不能在远端再次叠层，只由受伤玩家自己的客户端记录。
            if (!HasAdversityAmulet || Player.whoAmI != Main.myPlayer ||
                info.Cancelled || info.Damage <= 0)
                return;

            AttackKind kind;
            int type;
            if (info.DamageSource.SourceProjectileType > 0)
            {
                // 使用伤害来源保存的弹幕类型，弹幕消失后也不误判成环境伤害。
                kind = AttackKind.Projectile;
                type = info.DamageSource.SourceProjectileType;
            }
            else if (info.DamageSource.TryGetCausingEntity(out Entity entity) && entity is NPC npc)
            {
                kind = AttackKind.NpcContact;
                type = npc.type;
            }
            else
            {
                ClearAdaptation();
                return;
            }

            Stacks = _attackKind == kind && _attackType == type
                ? System.Math.Min(Stacks + 1, MaxStacks) : 1;
            _attackKind = kind;
            _attackType = type;
            RemainingFrames = StackDuration;
            UpdateDisplayBuff();
        }

        public override void UpdateDead() => ClearAdaptation();
        public override void OnEnterWorld() => ClearAdaptation();

        private void UpdateDisplayBuff()
        {
            int type = ModContent.BuffType<AdversityAdaptationBuff>();
            int index = Player.FindBuffIndex(type);
            if (index < 0)
            {
                Player.AddBuff(type, RemainingFrames);
                index = Player.FindBuffIndex(type);
            }
            // Buff 只镜像玩家计时，不在其 Update 中执行属性或再次递减状态。
            if (index >= 0)
                Player.buffTime[index] = RemainingFrames;
        }

        private void ClearAdaptation()
        {
            Stacks = 0;
            RemainingFrames = 0;
            _attackKind = AttackKind.None;
            _attackType = 0;
            Player.ClearBuff(ModContent.BuffType<AdversityAdaptationBuff>());
        }
    }
}
