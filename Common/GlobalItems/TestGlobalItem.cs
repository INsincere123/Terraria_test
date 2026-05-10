using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using TestMod.Rarities;
using TestMod.Common.Players;
using TestMod.Items.DamageTypes;
using Terraria.Utilities;

namespace TestMod.Common.GlobalItems
{
    /// <summary>
    /// 全局物品钩子：武器面板强化（伤害、击退、发射逻辑）
    /// </summary>
    public class TestGlobalItem : GlobalItem
    {
        // ══════════════════════════════════════════════════════════════
        //   SetDefaults — 固定数值调整（不受 godMode 开关影响）
        // ══════════════════════════════════════════════════════════════
        public override void SetDefaults(Item item)
        {
            // ──────────────────────────────────────────
            //   钩爪：应用 Bobbit Hook 数据
            // ──────────────────────────────────────────
            // 紫晶钩（ID 1236）— 飞出速度沿用 Bobbit Hook
            if (item.type == ItemID.AmethystHook)
                item.shootSpeed = 25f;

            // 沙漠虎杖：提升基础数值
            if (item.type == ItemID.StormTigerStaff)
            {
                item.damage = 51;
                item.knockBack = 10;
                item.useTime = 20;
                item.useAnimation = 20;
            }

            // 阿比盖尔之花：提升基础数值
            if (item.type == ItemID.AbigailsFlower)
            {
                item.damage = 14;
                item.knockBack = 2;
                item.mana = 0;
                item.useTime = 20;
                item.useAnimation = 20;
            }

        }

        /// <summary>神模开关检查</summary>
        private bool IsGodMode(Player player) =>
            player?.active == true && player.GetModPlayer<CorePlayer>().godModeBuff;

        // ══════════════════════════════════════════════════════════════
        //   PreDrawTooltipLine — 拦截物品名称行，替换为自定义稀有度绘制
        // ══════════════════════════════════════════════════════════════
        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (line.Mod != "Terraria") return true;

            // ── 物品名称行：自定义稀有度效果 ─────────────────────────
            if (line.Name == "ItemName")
            {
                if (item.rare == ModContent.RarityType<AntaresRarity>())
                {
                    AntaresRarity.Draw(item, line);
                    return false;
                }
                return true;
            }

            // ── 伤害行：各职业自定义动态效果 ─────────────────────────
            if (line.Name == "Damage")
            {
                var   sb   = Main.spriteBatch;
                float time = Main.GlobalTimeWrappedHourly;
                var   r    = line.Rotation;
                var   o    = line.Origin;
                var   s    = line.BaseScale;
                int   x    = line.X, y = line.Y;
                string txt = line.Text;

                DamageClass dmg = item.DamageType;
                if      (dmg == Items.DamageTypes.TrueDamageClass.Instance)
                    DamageLineRenderer.DrawTrue  (sb, txt, x, y, r, o, s, time);
                else if (dmg == DamageClass.Melee || dmg == DamageClass.MeleeNoSpeed)
                    DamageLineRenderer.DrawMelee (sb, txt, x, y, r, o, s, time);
                else if (dmg == DamageClass.Ranged)
                    DamageLineRenderer.DrawRanged(sb, txt, x, y, r, o, s, time);
                else if (dmg == DamageClass.Magic)
                    DamageLineRenderer.DrawMagic (sb, txt, x, y, r, o, s, time);
                else if (dmg == DamageClass.Summon || dmg == DamageClass.SummonMeleeSpeed)
                    DamageLineRenderer.DrawSummon(sb, txt, x, y, r, o, s, time);
                else
                    return true; // 其他类型保持默认

                return false; // 阻止引擎默认绘制
            }

            return true;
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyWeaponDamage — 伤害倍率（godMode 开启时生效）
        // ══════════════════════════════════════════════════════════════
        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            if (!IsGodMode(player)) return;

            // 🏹 幻影弓 ×3.6
            if (item.type == ItemID.Phantasm)
                damage *= 3.6f;

            // ☀️ 破晓之光 ×14
            else if (item.type == ItemID.DayBreak)
                damage *= 14f;

            // 星云烈焰 ×1.8
            else if (item.type == ItemID.NebulaBlaze)
                damage *= 1.8f;

            // 🌟 召唤法杖
            else if (item.DamageType == DamageClass.Summon)
            {
                if (item.type == ItemID.StardustDragonStaff) damage *= 11f;
                else if (item.type == ItemID.StardustCellStaff) damage *= 18f;
                else if (item.type == ItemID.MoonlordTurretStaff) damage *= 15f;
                else if (item.type == ItemID.RainbowCrystalStaff) damage *= 15f;
                else if (item.type == ItemID.EmpressBlade) damage *= 15f;
            }
            
            // 🪢 万花筒 ×6.3
            else if (item.type == ItemID.RainbowWhip)
                damage *= 6.3f;
        }

        // ══════════════════════════════════════════════════════════════
        //   ChoosePrefix — 真实伤害武器前缀池（职业无关，独立于伤害继承）
        // ══════════════════════════════════════════════════════════════
        // 前缀池设计：
        //   · 炼化（自定义）出现 2 次 → 约 8% 概率（相对稀有的高阶词缀）
        //   · 其余为原版通用/近战正面词缀
        //   · 未包含负面词缀；如需加入，在数组末尾追加 PrefixID.Broken 等
        //
        // RefinementPrefix.CanRoll() 已限制炼化只能出现在真实伤害武器上
        public override int ChoosePrefix(Item item, UnifiedRandom rand)
        {
            if (item.DamageType != TrueDamageClass.Instance || item.damage <= 0)
                return -1;

            int refinement = ModContent.GetInstance<Prefixes.RefinementPrefix>().Type;

            // ── 通用词缀（Universal，适用于所有武器）────────────────────
            // ── 公共词缀（Common，有攻速修正，但不限定职业）─────────────
            // 炼化出现 2 次（约 7.7%）；正负词缀均保留，与原版重铸行为一致
            int[] pool =
            [
                //refinement, refinement,
                // 通用（14个）
                PrefixID.Keen,      PrefixID.Superior,  PrefixID.Forceful,
                PrefixID.Broken,    PrefixID.Damaged,   PrefixID.Shoddy,
                PrefixID.Hurtful,   PrefixID.Strong,    PrefixID.Unpleasant,
                PrefixID.Weak,      PrefixID.Ruthless,  PrefixID.Godly,
                PrefixID.Demonic,   PrefixID.Zealous,
                // 公共（含攻速修正，10个）
                PrefixID.Quick,     PrefixID.Deadly,    PrefixID.Agile,
                PrefixID.Nimble,    PrefixID.Murderous, PrefixID.Slow,
                PrefixID.Sluggish,  PrefixID.Lazy,      PrefixID.Annoying,
                PrefixID.Nasty,
            ];

            return pool[rand.Next(pool.Length)];
        }

        // ══════════════════════════════════════════════════════════════
        //   UseTimeMultiplier — 暴走攻速×5 / 虚弱攻速×0.5
        // ══════════════════════════════════════════════════════════════
        public override float UseTimeMultiplier(Item item, Player player)
        {
            var mp = player.GetModPlayer<BloodFeedPlayer>();
            if (mp.IsBerserk)   return 1f / 5f;  // ×5 攻速 → useTime × 0.2
            if (mp.IsExhausted) return 2f;        // ×0.5 攻速 → useTime × 2
            return 1f;
        }

        public override float UseAnimationMultiplier(Item item, Player player)
        {
            var mp = player.GetModPlayer<BloodFeedPlayer>();
            if (mp.IsBerserk)   return 1f / 5f;
            if (mp.IsExhausted) return 2f;
            return 1f;
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyHitNPC — 真实伤害：无视目标防御 + 暴走无视防御
        // ══════════════════════════════════════════════════════════════
        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (item.DamageType == TrueDamageClass.Instance)
                modifiers.ScalingArmorPenetration += 1f;

            // 暴走期间所有武器无视防御
            var mp = player.GetModPlayer<BloodFeedPlayer>();
            if (mp.IsBerserk)
                modifiers.ScalingArmorPenetration += 1f;
        }

        // ══════════════════════════════════════════════════════════════
        //   ModifyWeaponKnockback — 击退倍率（godMode 开启时生效）
        // ══════════════════════════════════════════════════════════════
        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
        {
            if (!IsGodMode(player)) return;

            if (item.type == ItemID.DayBreak) knockback *= 2f;
            else if (item.type == ItemID.StardustCellStaff) knockback *= 2f;
            else if (item.type == ItemID.MoonlordTurretStaff) knockback *= 5f;
            else if (item.type == ItemID.RainbowCrystalStaff) knockback *= 2f;
            else if (item.type == ItemID.EmpressBlade) knockback *= 2f;
        }

        // ══════════════════════════════════════════════════════════════
        //   Shoot — 幻影弓：把所有箭矢转换为自定义强化弹射物
        //   参考灾厄 Monsoon 的做法，以玩家持握点为发射源
        //   生成2支扇形散射箭，各自独立追踪/破甲/分裂
        // ══════════════════════════════════════════════════════════════
        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (item.type != ItemID.Phantasm || !IsGodMode(player))
                return true;

            // 只生成弓体 holdout，由它负责发箭
            if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.PhantasmHoldout>()] <= 0)
            {
                Projectile.NewProjectile(source, position, velocity,
                    ModContent.ProjectileType<Projectiles.PhantasmHoldout>(),
                    damage, knockback, player.whoAmI);
            }
            return false;
        }
    }
}
