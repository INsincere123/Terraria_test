using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 战士之证：受伤后 5 秒内获得 10% 免伤（每次受伤刷新计时）。
    /// 冰冻护盾使用原版冰障与圣骑士保护，避免与独立原版来源重复结算。
    /// </summary>
    public class WarriorSealPlayer : ModPlayer
    {
        private const int DamageReductionTicks = 300; // 5 秒
        private const float DamageReduction = 0.1f;

        private bool equipped;
        private int _damageReductionTimer;

        internal void Equip() => equipped = true;

        public override void ResetEffects() => equipped = false;

        public override void OnHurt(Player.HurtInfo info)
        {
            if (equipped && info.Damage > 0) _damageReductionTimer = DamageReductionTicks;
        }

        public override void PostUpdateMiscEffects()
        {
            // 卸装即清窗口，不残留免伤。
            if (!equipped)
            {
                _damageReductionTimer = 0;
                return;
            }
            ApplyFrozenShield();
            if (_damageReductionTimer > 0)
            {
                Player.endurance += DamageReduction;
                _damageReductionTimer--;
            }
        }

        private void ApplyFrozenShield()
        {
            // 原版 Buff 早于装备更新；首次装备和最终生命上限变化时补齐当前帧，
            // 原版已结算或其他饰品已提供冰障时不重复增加 25% 免伤。
            if (Player.statLife <= Player.statLifeMax2 * 0.5f)
            {
                Player.AddBuff(BuffID.IceBarrier, 5);
                if (!Player.iceBarrier)
                {
                    Player.iceBarrier = true;
                    Player.endurance += 0.25f;
                    if (++Player.iceBarrierFrameCounter > 2)
                    {
                        Player.iceBarrierFrameCounter = 0;
                        Player.iceBarrierFrame = (byte)((Player.iceBarrierFrame + 1) % 12);
                    }
                }
            }
            // 原版圣骑士护盾：自身生命高于 25% 才为同队、800 像素内队友提供保护。
            if (Player.statLife <= Player.statLifeMax2 * 0.25f) return;
            Player.hasPaladinShield = true;
            if (Main.dedServ || Player.whoAmI == Main.myPlayer || Player.miscCounter % 10 != 0) return;
            Player local = Main.LocalPlayer;
            if (local.active && local.team != 0 && local.team == Player.team &&
                Vector2.Distance(Player.position, local.position) < 800f)
                local.AddBuff(BuffID.PaladinsShield, 20);
        }

        public override void UpdateDead() => _damageReductionTimer = 0;
        public override void OnEnterWorld() => _damageReductionTimer = 0;
    }
}
