using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Microsoft.Xna.Framework; // MathHelper
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    public class CorePlayer : ModPlayer
    {
        public bool godModeBuff  = false; // 开关1
        public bool godModeBuff2 = false; // 开关2

        // 击败月亮领主后自动解锁 godModeBuff 的一次性标志（已保存）
        private bool godModeUnlocked = false;

        // 暴击伤害加成系数（由 godModeBuff2 驱动，0.5 = +50%）
        public float critDamageBonus = 0f;

        // ── 输出伤害乘区（防御前结算，每帧重置为 1f）────────────────────────────
        // 独立增伤：在加法增伤（Additive）之后独立相乘，作用于 SourceDamage.Multiplicative
        // 敌人加深：从目标侧乘算，作用于 TargetDamageMultiplier
        // 调用示例（UpdateAccessory 里）：
        //   player.GetModPlayer<CorePlayer>().independentDamageMult  *= 1.3f;  // 独立增伤 +30%
        //   player.GetModPlayer<CorePlayer>().targetVulnerabilityMult *= 1.2f;  // 敌人伤害加深 +20%
        //   两者可单独使用，也可同时使用。
        public float independentDamageMult   = 1f;  // 独立增伤乘区
        public float targetVulnerabilityMult  = 1f;  // 敌人伤害加深乘区

        // ── 受击伤害乘区（默认 1f = 不生效，赋值后在结算最终伤害时相乘，独立乘区）──
        // 使用方法：在 UpdateAccessory / UpdateEquip 里设置，默认 0.75f 即减少 25%
        // 示例：player.GetModPlayer<CorePlayer>().projDamageMultiplier = 0.75f;
        public float projDamageMultiplier = 1f;     // 弹幕伤害乘区
        public float npcDamageMultiplier  = 1f;     // NPC 伤害乘区

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
            // ── 受击伤害乘区重置 ─────────────────────────────────
            projDamageMultiplier = 1f;
            npcDamageMultiplier  = 1f;

            // ── 输出伤害乘区重置 ─────────────────────────────────
            independentDamageMult  = 1f;
            targetVulnerabilityMult = 1f;

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
        //   PostUpdateEquips — 将鞭子词缀的 scaleMult（Item.scale）
        //   桥接到 whipRangeMultiplier，使鞭子范围随词缀正确变化。
        //   原因：Item.scale 只影响近战 hitbox，对鞭子范围无效；
        //         鞭子范围由 whipRangeMultiplier 驱动，需手动桥接。
        // ██████████████████████████████████████████████████████████████
        // ██████████████████████████████████████████████████████████████
        //   PostUpdateMiscEffects — 两个输出伤害乘区统一在 Stat 阶段写入
        //   写入 GetDamage(Generic).Multiplicative，与命中阶段各 hook 完全隔离
        // ██████████████████████████████████████████████████████████████
        public override void PostUpdateMiscEffects()
        {
            if (independentDamageMult != 1f)
                Player.GetDamage(DamageClass.Generic) *= independentDamageMult;
            if (targetVulnerabilityMult != 1f)
                Player.GetDamage(DamageClass.Generic) *= targetVulnerabilityMult;
        }

        public override void PostUpdateEquips()
        {
            Item held = Player.HeldItem;

            // 鞭子词缀 scaleMult 桥接：Item.scale → whipRangeMultiplier
            if (held.DamageType == DamageClass.SummonMeleeSpeed
                && held.shoot > 0
                && ProjectileID.Sets.IsAWhip[held.shoot]
                && held.scale != 1f)
            {
                Player.whipRangeMultiplier *= held.scale;
            }

            // godMode 开启时万花筒范围 ×1.5
            if (godModeBuff && held.type == ItemID.RainbowWhip)
            {
                Player.whipRangeMultiplier *= 1.5f;
            }
        }

        // ██████████████████████████████████████████████████████████████
        //   ModifyHitNPC — 近战/直接命中的暴击伤害加成
        //   只在暴击时生效，非暴击命中不影响
        // ██████████████████████████████████████████████████████████████
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (critDamageBonus > 0f)
                modifiers.CritDamage += critDamageBonus;
        }

        // ██████████████████████████████████████████████████████████████
        //   ModifyHitNPCWithProj — 弹射物命中的暴击伤害加成
        //   覆盖所有弹射物（矛、子弹、召唤物弹射物等）
        // ██████████████████████████████████████████████████████████████
        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (critDamageBonus > 0f)
                modifiers.CritDamage += critDamageBonus;
        }

        // ██████████████████████████████████████████████████████████████
        //   受击伤害乘区 — 弹幕伤害
        //   默认 1f 不生效，赋值 0.75f 即减少 25% 弹幕伤害
        // ██████████████████████████████████████████████████████████████
        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers)
        {
            if (projDamageMultiplier != 1f)
                modifiers.FinalDamage *= projDamageMultiplier;
        }

        // ██████████████████████████████████████████████████████████████
        //   受击伤害乘区 — NPC 伤害
        //   默认 1f 不生效，赋值 0.75f 即减少 25% NPC 伤害
        // ██████████████████████████████████████████████████████████████
        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers)
        {
            if (npcDamageMultiplier != 1f)
                modifiers.FinalDamage *= npcDamageMultiplier;
        }

        // ██████████████████████████████████████████████████████████████
        //   PostUpdate — 月亮领主击败后自动解锁 godModeBuff（仅一次）
        //   条件：NPC.downedMoonlord == true 且尚未解锁过
        //   解锁后由物品开关自由控制，不会被再次强制开启
        // ██████████████████████████████████████████████████████████████
        public override void PostUpdate()
        {
            if (NPC.downedMoonlord && !godModeUnlocked)
            {
                godModeUnlocked = true;
                godModeBuff     = true;
            }
        }

        // ██████████████████████████████████████████████████████████████
        //   存档持久化：godModeBuff（当前开关状态）+ godModeUnlocked（解锁标志）
        // ██████████████████████████████████████████████████████████████
        public override void SaveData(TagCompound tag)
        {
            tag["godModeBuff"]    = godModeBuff;
            tag["godModeUnlocked"] = godModeUnlocked;
        }

        public override void LoadData(TagCompound tag)
        {
            godModeBuff    = tag.GetBool("godModeBuff");
            godModeUnlocked = tag.GetBool("godModeUnlocked");
        }
    }
}