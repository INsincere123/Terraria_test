using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;

namespace TestMod.Common.Players
{
    /// <summary>超能护盾只负责低血阈值和冷却；临时量及衰减归核心护盾池管理。</summary>
    public class SuperEnergyShieldPlayer : ModPlayer
    {
        public const int EmergencyFrames = 270;
        public const int CooldownFrames = 120 * 60;
        public int Cooldown { get; private set; }
        internal bool IsEquipped;
        private float _prevLifeFrac = 1f;

        public override void ResetEffects() => IsEquipped = false;

        public override void PostUpdate()
        {
            EnergyShieldPlayer shield = Player.GetModPlayer<EnergyShieldPlayer>();
            if (shield.IsAuthority)
            {
                TickCooldown(shield);
                CheckEmergencyTrigger();
            }
            RefreshCooldownBuff();
        }

        internal void CheckEmergencyTrigger()
        {
            EnergyShieldPlayer shield = Player.GetModPlayer<EnergyShieldPlayer>();
            if (!shield.IsAuthority) return;
            float lifeFrac = (float)Player.statLife / Math.Max(1, Player.statLifeMax2);
            if (IsEquipped && !Player.dead && lifeFrac < 0.30f && _prevLifeFrac >= 0.30f && Cooldown == 0)
            {
                Cooldown = CooldownFrames;
                shield.AddTemporaryShield(Player.statLifeMax2 * 0.5f + Player.statDefense, EmergencyFrames);
            }
            _prevLifeFrac = lifeFrac;
        }

        public override void UpdateDead()
        {
            IsEquipped = false;
            EnergyShieldPlayer shield = Player.GetModPlayer<EnergyShieldPlayer>();
            if (shield.IsAuthority)
            {
                TickCooldown(shield);
                _prevLifeFrac = 0f; // 复活的低生命值不是一次跌破阈值的伤害。
            }
        }

        private void TickCooldown(EnergyShieldPlayer shield)
        {
            if (Cooldown > 0)
            {
                Cooldown--;
                shield.SendShieldState(force: Cooldown == 0);
            }
        }

        private void RefreshCooldownBuff()
        {
            if (Main.netMode == NetmodeID.Server) return;
            int type = ModContent.BuffType<SuperEnergyShieldCooldownBuff>();
            if (Cooldown > 0)
                Player.AddBuff(type, Cooldown + 1);
            else
                Player.ClearBuff(type);
        }

        internal void ApplySyncedCooldown(int remaining) => Cooldown = remaining;
    }
}
