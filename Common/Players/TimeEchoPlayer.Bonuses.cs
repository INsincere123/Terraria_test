using System;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    public sealed partial class TimeEchoPlayer
    {
        private const int RewindBonusDuration = 300;
        private const int SwapBonusDuration = 180;
        private int rewindBonusTicks;
        private int swapBonusTicks;
        private ulong bonusStartTick = ulong.MaxValue;

        // 临时加成由技能成功事件开启，不持久化；死亡、移除、切世界统一清理。
        private void StartBonuses(bool rewind)
        {
            if (rewind) rewindBonusTicks = RewindBonusDuration;
            else swapBonusTicks = SwapBonusDuration;
            bonusStartTick = Main.GameUpdateCount;
        }

        private void ClearBonuses()
        {
            rewindBonusTicks = swapBonusTicks = 0;
            bonusStartTick = ulong.MaxValue;
        }

        private void TickBonuses()
        {
            if (Player.dead || !HasAbility)
            {
                ClearBonuses();
                return;
            }
            // 玩家属性/生命再生已结算完再递减，激活当帧不扣持续时间。
            if (bonusStartTick == Main.GameUpdateCount) return;
            if (rewindBonusTicks > 0) rewindBonusTicks--;
            if (swapBonusTicks > 0) swapBonusTicks--;
        }

        private void SyncBonuses(int rewind, int swap)
        {
            if (Player.dead || !HasAbility)
            {
                ClearBonuses();
                return;
            }
            rewindBonusTicks = Math.Clamp(rewind, 0, RewindBonusDuration);
            swapBonusTicks = Math.Clamp(swap, 0, SwapBonusDuration);
        }

        public override void PostUpdateEquips()
        {
            if (Player.dead || !HasAbility) return;
            if (rewindBonusTicks > 0) Player.endurance += 0.20f;
            // 与本项目 IndependentDamage 约定一致，写乘法区而不是通用加算区。
            if (swapBonusTicks > 0) Player.GetDamage(DamageClass.Generic) *= 1.2f;
            if (swapBonusTicks > 0) Player.GetArmorPenetration(DamageClass.Generic) += 25;
        }

        public override void UpdateLifeRegen()
        {
            if (!Player.dead && HasAbility && rewindBonusTicks > 0)
                Player.lifeRegen += 16;
        }
    }
}
