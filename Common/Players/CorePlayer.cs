using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework; // MathHelper
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    public class CorePlayer : ModPlayer
    {
        public bool godModeBuff  = false; // 开关1
        public bool godModeBuff2 = false; // 开关2

        // 暴击伤害加成系数（由 godModeBuff2 驱动，0.5 = +50%）
        public float critDamageBonus = 0f;

        // ── 上一帧开关状态，用于检测切换瞬间 ────────────────
        private bool _prevGodModeBuff = false;

        // ── 开启前的原版稀有度备份（背包索引 → 原始 rare）──
        private readonly Dictionary<int, int> _originalRarity = new();

        // ╔══════════════════════════════════════════════════════╗
        // ║         godMode 稀有度调整区域                       ║
        // ║  GodModeRarity  开启 godMode 时的稀有度（无灾厄时） ║
        // ║  原版稀有度参考：                                    ║
        // ║    -1=灰  0=白  1=蓝  2=绿  3=橙  4=浅红            ║
        // ║    5=粉   6=黄  7=青  8=深红  9=紫  10=红  11=琥珀   ║
        // ╚══════════════════════════════════════════════════════╝
        public const int GodModeRarity = 11; // ← godMode 开启时的稀有度（无灾厄时）

        // godMode 影响的物品列表
        private static readonly int[] GodModeItems =
        {
            ItemID.Phantasm,
            ItemID.DayBreak,
            ItemID.NebulaBlaze,
            ItemID.StardustDragonStaff,
            ItemID.StardustCellStaff,
            ItemID.MoonlordTurretStaff,
            ItemID.RainbowCrystalStaff,
            ItemID.EmpressBlade,
            ItemID.RainbowWhip,
        };

        public override void ResetEffects()
        {
            // ══════════════════════════════════════════════════════════
            // Buff1：小幅强化
            // ══════════════════════════════════════════════════════════
            if (godModeBuff)
            {
                Player.statDefense                               += 3;
                Player.statLifeMax2                             += 10;
                Player.endurance                                += 0.02f;
                Player.maxMinions                               += 1;
                Player.maxTurrets                               += 1;
                Player.GetDamage(DamageClass.Generic)           += 0.03f;
                Player.GetArmorPenetration(DamageClass.Generic) += 12;
                Player.GetAttackSpeed(DamageClass.Generic)      += 0.05f;
            }

            // ══════════════════════════════════════════════════════════
            // Buff2：神模强化 + 暴击加成
            // ══════════════════════════════════════════════════════════
            if (godModeBuff2)
            {
                Player.statDefense                               += 6666;
                Player.statLifeMax2                             += 6666;
                Player.statManaMax2                             += 666;
                Player.endurance                                 = MathHelper.Clamp(Player.endurance + 0.66f, 0f, 0.999f);
                Player.moveSpeed                                += 0.1f;
                Player.maxMinions                               += 21;
                Player.maxTurrets                               += 9;
                Player.GetDamage(DamageClass.Generic)           += 1.23f;
                Player.GetArmorPenetration(DamageClass.Generic) += 3600;
                Player.GetAttackSpeed(DamageClass.Generic)      += 1f;

                // 全属性暴击率 +25%
                Player.GetCritChance(DamageClass.Generic)       += 25;

                // 暴击伤害 +50%，在 ModifyHitNPC / ModifyHitNPCWithProj 里应用
                critDamageBonus = 1f;
            }
            else
            {
                // godModeBuff2 关闭时重置，防止残留
                critDamageBonus = 0f;
            }

            // ── 只检测 godModeBuff 切换，触发一次背包稀有度更新 ─
            if (godModeBuff != _prevGodModeBuff)
            {
                if (godModeBuff)
                    ApplyGodModeRarity();
                else
                    RestoreOriginalRarity();
            }

            _prevGodModeBuff = godModeBuff;
        }

        // ── 开启时：备份原版稀有度，然后覆盖 ────────────────────
        private void ApplyGodModeRarity()
        {
            int onRarity = CalamityCompatSystem.CalamityLoaded
                ? CalamityCompatSystem.CalamityRarity
                : GodModeRarity;

            _originalRarity.Clear();

            for (int i = 0; i < Player.inventory.Length; i++)
            {
                Item item = Player.inventory[i];
                if (item == null || item.IsAir) continue;

                foreach (int id in GodModeItems)
                {
                    if (item.type == id)
                    {
                        _originalRarity[i] = item.rare; // 备份原版稀有度
                        item.rare = onRarity;
                        break;
                    }
                }
            }
        }

        // ── 关闭时：还原各物品原本的稀有度 ──────────────────────
        private void RestoreOriginalRarity()
        {
            foreach (var kv in _originalRarity)
            {
                int i = kv.Key;
                if (i >= Player.inventory.Length) continue;

                Item item = Player.inventory[i];
                if (item == null || item.IsAir) continue;

                // 确认该格还是同一类物品再还原，防止物品被移走后错误还原
                bool isTarget = false;
                foreach (int id in GodModeItems)
                {
                    if (item.type == id) { isTarget = true; break; }
                }
                if (isTarget)
                    item.rare = kv.Value;
            }

            _originalRarity.Clear();
        }

        // ██████████████████████████████████████████████████████████████
        //   ModifyHitNPC — 近战/直接命中的暴击伤害加成
        //   只在暴击时生效，非暴击命中不影响
        // ██████████████████████████████████████████████████████████████
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (critDamageBonus <= 0f) return;

            modifiers.CritDamage += critDamageBonus;
        }

        // ██████████████████████████████████████████████████████████████
        //   ModifyHitNPCWithProj — 弹射物命中的暴击伤害加成
        //   覆盖所有弹射物（矛、子弹、召唤物弹射物等）
        // ██████████████████████████████████████████████████████████████
        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (critDamageBonus <= 0f) return;

            modifiers.CritDamage += critDamageBonus;
        }
    }
}