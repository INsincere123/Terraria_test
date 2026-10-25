using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Content.Buffs;

namespace TestMod.Common.Players
{
    public class ClassicGrapeBeerPlayer : ModPlayer
    {
        private bool penaltyApplied;

        public override void ResetEffects() => penaltyApplied = false;

        internal void ApplyPenalty()
        {
            if (penaltyApplied) return;
            penaltyApplied = true;
            Player.GetDamage(DamageClass.Generic) -= ClassicGrapeBeerBuff.DamagePenalty;
        }

        internal static bool HasTrackingEffect(Player player)
            => player.HasBuff(ModContent.BuffType<ClassicGrapeBeerBuff>()) ||
                player.HasBuff(ModContent.BuffType<SmartScopeActiveBuff>());

        public override void PreUpdateBuffs()
        {
            // 在任何 Buff.Update 前排除原版，避免其玩家标志、暴伤和酒精系统被重复注入。
            if (HasTrackingEffect(Player))
                ClassicGrapeBeerSystem.RemoveOriginalBuff(Player);
        }
    }
}
