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
        //   ChoosePrefix — 加权前缀池（真实伤害 + 可扩展至其他职业）
        // ══════════════════════════════════════════════════════════════
        // 权重规则（还原原版行为）：
        //   正面/中性词缀       weight = 10
        //   ReducedNaturalChance 词缀（负面）  weight = 3  （原版有 66% 拒绝率，10×34%≈3）
        //   炼化（自定义极稀有） weight = 1   （正面词缀的 1/10）
        public override int ChoosePrefix(Item item, UnifiedRandom rand)
        {
            // ── 真实伤害武器 ──────────────────────────────────────────
            if (item.DamageType == TrueDamageClass.Instance && item.damage > 0)
            {
                int r = ModContent.GetInstance<Prefixes.RefinementPrefix>().Type;
                // (prefixId, weight) — ReducedNaturalChance 的词缀标注 w=3
                (int id, int weight)[] pool =
                [
                    // 通用词缀（Universal）
                    (PrefixID.Keen,      10), (PrefixID.Superior,  10), (PrefixID.Forceful,  10),
                    (PrefixID.Broken,     3), (PrefixID.Damaged,    3), (PrefixID.Shoddy,     3), // ReducedNaturalChance
                    (PrefixID.Hurtful,   10), (PrefixID.Strong,    10), (PrefixID.Unpleasant,10),
                    (PrefixID.Weak,       3), (PrefixID.Ruthless,  10), (PrefixID.Godly,     10), // Weak=ReducedNaturalChance
                    (PrefixID.Demonic,   10), (PrefixID.Zealous,   10),
                    // 公共词缀（Common，含攻速修正）
                    // Deadly2=43 是通用版；Deadly=20 是近战专属（有尺寸修正），两者不同
                    (PrefixID.Quick,     10), (PrefixID.Deadly2,   10), (PrefixID.Agile,     10),
                    (PrefixID.Nimble,    10), (PrefixID.Murderous, 10), (PrefixID.Slow,       3), // ReducedNaturalChance
                    (PrefixID.Sluggish,   3), (PrefixID.Lazy,       3), (PrefixID.Annoying,  10), // ReducedNaturalChance
                    (PrefixID.Nasty,     10),
                    // 炼化（自定义，极稀有）
                    (r,                   1),
                ];
                return WeightedRandomPrefix(rand, pool);
            }

            if (item.damage <= 0) return -1;

            // ── 鞭子（SummonMeleeSpeed）——先于 Summon/Melee 判断 ─────
            if (item.CountsAsClass(DamageClass.SummonMeleeSpeed))
            {
                int r = ModContent.GetInstance<Prefixes.RefinementWhipPrefix>().Type;
                (int id, int weight)[] pool =
                [
                    (PrefixID.Keen,10),(PrefixID.Superior,10),(PrefixID.Forceful,10),
                    (PrefixID.Broken,3),(PrefixID.Damaged,3),(PrefixID.Shoddy,3),
                    (PrefixID.Hurtful,10),(PrefixID.Strong,10),(PrefixID.Unpleasant,10),
                    (PrefixID.Weak,3),(PrefixID.Ruthless,10),(PrefixID.Godly,10),
                    (PrefixID.Demonic,10),(PrefixID.Zealous,10),
                    (PrefixID.Quick,10),(PrefixID.Deadly2,10),(PrefixID.Agile,10),
                    (PrefixID.Nimble,10),(PrefixID.Murderous,10),(PrefixID.Slow,3),
                    (PrefixID.Sluggish,3),(PrefixID.Lazy,3),(PrefixID.Annoying,10),(PrefixID.Nasty,10),
                    // 近战系：Dangerous Savage Sharp Bulky Heavy Light Celestial Furious
                    (PrefixID.Dangerous,10),(PrefixID.Savage,10),(PrefixID.Sharp,10),
                    (PrefixID.Bulky,10),(PrefixID.Heavy,10),(PrefixID.Light,10),
                    (PrefixID.Celestial,10),(PrefixID.Furious,10),
                    // 鞭子炼化
                    (r,1),
                ];
                return WeightedRandomPrefix(rand, pool);
            }

            // ── 召唤（非鞭子）────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Summon))
            {
                bool hasKB = item.knockBack > 0f;
                int r = hasKB
                    ? ModContent.GetInstance<Prefixes.RefinementSummonPrefix>().Type
                    : ModContent.GetInstance<Prefixes.RefinementSummonNoKBPrefix>().Type;
                // 原版召唤武器词缀池极小（Godly/Demonic/Ruthless/Mythical）
                // 此处适度扩展，保留召唤不能暴击的设计（无 critBonus 类词缀）
                (int id, int weight)[] pool = hasKB
                    ? [
                        (PrefixID.Godly,10),(PrefixID.Demonic,10),(PrefixID.Ruthless,10),
                        (PrefixID.Hurtful,10),(PrefixID.Strong,10),(PrefixID.Keen,10),
                        (PrefixID.Zealous,10),(PrefixID.Broken,3),(PrefixID.Damaged,3),
                        (PrefixID.Weak,3),(PrefixID.Quick,10),(PrefixID.Nimble,10),
                        (PrefixID.Slow,3),(PrefixID.Sluggish,3),
                        (PrefixID.Mythical,10), // 83 最优召唤词缀
                        (r,1),
                    ]
                    : [
                        (PrefixID.Demonic,10),(PrefixID.Ruthless,10),
                        (PrefixID.Hurtful,10),(PrefixID.Keen,10),
                        (PrefixID.Zealous,10),(PrefixID.Broken,3),(PrefixID.Damaged,3),
                        (PrefixID.Quick,10),(PrefixID.Nimble,10),(PrefixID.Slow,3),
                        (PrefixID.Mythical,10),
                        (r,1),
                    ];
                return WeightedRandomPrefix(rand, pool);
            }

            // ── 魔法 ──────────────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Magic) && item.mana > 3)
            {
                bool hasKB = item.knockBack > 0f;
                int r = hasKB
                    ? ModContent.GetInstance<Prefixes.RefinementMagicPrefix>().Type
                    : ModContent.GetInstance<Prefixes.RefinementMagicNoKBPrefix>().Type;
                // 魔法专属：Mystic(26) Adept(27) Masterful(28) Intense(32) Taboo(33)
                // 负面（ReducedNaturalChance）：Inept(29) Ignorant(30) Deranged(31)
                // 最优：Mythical(83)
                var basePool = new (int id, int weight)[]
                {
                    (PrefixID.Keen,10),(PrefixID.Superior,10),(PrefixID.Forceful,10),
                    (PrefixID.Broken,3),(PrefixID.Damaged,3),(PrefixID.Shoddy,3),
                    (PrefixID.Hurtful,10),(PrefixID.Unpleasant,10),
                    (PrefixID.Weak,3),(PrefixID.Ruthless,10),(PrefixID.Godly,10),
                    (PrefixID.Demonic,10),(PrefixID.Zealous,10),
                    (PrefixID.Quick,10),(PrefixID.Deadly2,10),(PrefixID.Agile,10),
                    (PrefixID.Nimble,10),(PrefixID.Murderous,10),(PrefixID.Slow,3),
                    (PrefixID.Sluggish,3),(PrefixID.Lazy,3),(PrefixID.Annoying,10),(PrefixID.Nasty,10),
                    (PrefixID.Mystic,10),(PrefixID.Adept,10),(PrefixID.Masterful,10),
                    (PrefixID.Inept,3),(PrefixID.Ignorant,3),(PrefixID.Deranged,3),
                    (PrefixID.Intense,10),(PrefixID.Taboo,10),
                    (PrefixID.Mythical,10),
                    (r,1),
                };
                var noKBPool = new (int id, int weight)[]
                {
                    (PrefixID.Keen,10),(PrefixID.Superior,10),
                    (PrefixID.Broken,3),(PrefixID.Damaged,3),(PrefixID.Shoddy,3),
                    (PrefixID.Hurtful,10),(PrefixID.Unpleasant,10),
                    (PrefixID.Weak,3),(PrefixID.Ruthless,10),(PrefixID.Godly,10),
                    (PrefixID.Demonic,10),(PrefixID.Zealous,10),
                    (PrefixID.Quick,10),(PrefixID.Deadly2,10),(PrefixID.Agile,10),
                    (PrefixID.Nimble,10),(PrefixID.Murderous,10),(PrefixID.Slow,3),
                    (PrefixID.Sluggish,3),(PrefixID.Lazy,3),(PrefixID.Annoying,10),(PrefixID.Nasty,10),
                    (PrefixID.Mystic,10),(PrefixID.Adept,10),(PrefixID.Masterful,10),
                    (PrefixID.Inept,3),(PrefixID.Ignorant,3),(PrefixID.Deranged,3),
                    (PrefixID.Intense,10),(PrefixID.Taboo,10),
                    (PrefixID.Mythical,10),
                    (r,1),
                };
                return WeightedRandomPrefix(rand, hasKB ? basePool : noKBPool);
            }

            // ── 远程 ──────────────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Ranged))
            {
                bool hasKB = item.knockBack > 0f;
                int r = hasKB
                    ? ModContent.GetInstance<Prefixes.RefinementRangedPrefix>().Type
                    : ModContent.GetInstance<Prefixes.RefinementRangedNoKBPrefix>().Type;
                // 远程专属：Sighted(16) Rapid(17) Hasty(18) Staunch(21) Powerful(25)
                // 负面（ReducedNaturalChance）：Awful(22) Lethargic(23) Awkward(24)
                // 最优：Unreal(82)
                var basePool = new (int id, int weight)[]
                {
                    (PrefixID.Keen,10),(PrefixID.Superior,10),(PrefixID.Forceful,10),
                    (PrefixID.Broken,3),(PrefixID.Damaged,3),(PrefixID.Shoddy,3),
                    (PrefixID.Hurtful,10),(PrefixID.Strong,10),(PrefixID.Unpleasant,10),
                    (PrefixID.Weak,3),(PrefixID.Ruthless,10),(PrefixID.Godly,10),
                    (PrefixID.Demonic,10),(PrefixID.Zealous,10),
                    (PrefixID.Quick,10),(PrefixID.Deadly2,10),(PrefixID.Agile,10),
                    (PrefixID.Nimble,10),(PrefixID.Murderous,10),(PrefixID.Slow,3),
                    (PrefixID.Sluggish,3),(PrefixID.Lazy,3),(PrefixID.Annoying,10),(PrefixID.Nasty,10),
                    (PrefixID.Sighted,10),(PrefixID.Rapid,10),(PrefixID.Hasty,10),
                    (PrefixID.Staunch,10),(PrefixID.Powerful,10),
                    (PrefixID.Awful,3),(PrefixID.Lethargic,3),(PrefixID.Awkward,3),
                    (PrefixID.Unreal,10),
                    (r,1),
                };
                var noKBPool = new (int id, int weight)[]
                {
                    (PrefixID.Keen,10),(PrefixID.Superior,10),
                    (PrefixID.Broken,3),(PrefixID.Damaged,3),(PrefixID.Shoddy,3),
                    (PrefixID.Hurtful,10),(PrefixID.Unpleasant,10),
                    (PrefixID.Weak,3),(PrefixID.Ruthless,10),(PrefixID.Godly,10),
                    (PrefixID.Demonic,10),(PrefixID.Zealous,10),
                    (PrefixID.Quick,10),(PrefixID.Deadly2,10),(PrefixID.Agile,10),
                    (PrefixID.Nimble,10),(PrefixID.Murderous,10),(PrefixID.Slow,3),
                    (PrefixID.Sluggish,3),(PrefixID.Lazy,3),(PrefixID.Annoying,10),(PrefixID.Nasty,10),
                    (PrefixID.Sighted,10),(PrefixID.Rapid,10),(PrefixID.Hasty,10),
                    (PrefixID.Staunch,10),(PrefixID.Powerful,10),
                    (PrefixID.Awful,3),(PrefixID.Lethargic,3),(PrefixID.Awkward,3),
                    (PrefixID.Unreal,10),
                    (r,1),
                };
                return WeightedRandomPrefix(rand, hasKB ? basePool : noKBPool);
            }

            // ── 近战 ──────────────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Melee) || item.CountsAsClass(DamageClass.MeleeNoSpeed))
            {
                // noUseGraphic = true → 矛/连枷/悠悠球（Other）；= false → 挥砍剑（Swing）
                bool isSwing = !item.noUseGraphic;
                int r = isSwing
                    ? ModContent.GetInstance<Prefixes.RefinementMeleeSwingPrefix>().Type
                    : ModContent.GetInstance<Prefixes.RefinementMeleeOtherPrefix>().Type;
                // 近战专属正面：Dangerous(3) Savage(4) Sharp(5) Pointy(6) Bulky(12)
                //              Heavy(14) Light(15) Intimidating(19) Deadly(20) Celestial(34) Furious(35)
                // 近战负面（ReducedNaturalChance）：Tiny(7) Terrible(8) Small(9) Dull(10) Unhappy(11) Shameful(13)
                // 尺寸专属（仅 Swing）：Large(1) Massive(2)；负面尺寸：Tiny(7) Small(9)
                // 最优：Legendary(81)
                var swingPool = new (int id, int weight)[]
                {
                    (PrefixID.Keen,10),(PrefixID.Superior,10),(PrefixID.Forceful,10),
                    (PrefixID.Broken,3),(PrefixID.Damaged,3),(PrefixID.Shoddy,3),
                    (PrefixID.Hurtful,10),(PrefixID.Strong,10),(PrefixID.Unpleasant,10),
                    (PrefixID.Weak,3),(PrefixID.Ruthless,10),(PrefixID.Godly,10),
                    (PrefixID.Demonic,10),(PrefixID.Zealous,10),
                    (PrefixID.Quick,10),(PrefixID.Deadly2,10),(PrefixID.Agile,10),
                    (PrefixID.Nimble,10),(PrefixID.Murderous,10),(PrefixID.Slow,3),
                    (PrefixID.Sluggish,3),(PrefixID.Lazy,3),(PrefixID.Annoying,10),(PrefixID.Nasty,10),
                    // 近战通用
                    (PrefixID.Dangerous,10),(PrefixID.Savage,10),(PrefixID.Sharp,10),
                    (PrefixID.Pointy,10),(PrefixID.Bulky,10),(PrefixID.Heavy,10),
                    (PrefixID.Light,10),(PrefixID.Intimidating,10),(PrefixID.Deadly,10),
                    (PrefixID.Celestial,10),(PrefixID.Furious,10),
                    (PrefixID.Terrible,3),(PrefixID.Dull,3),(PrefixID.Unhappy,3),(PrefixID.Shameful,3),
                    // 仅 Swing：尺寸修正
                    (PrefixID.Large,10),(PrefixID.Massive,10),
                    (PrefixID.Tiny,3),(PrefixID.Small,3),
                    (PrefixID.Legendary,10),
                    (r,1),
                };
                var otherPool = new (int id, int weight)[]
                {
                    (PrefixID.Keen,10),(PrefixID.Superior,10),(PrefixID.Forceful,10),
                    (PrefixID.Broken,3),(PrefixID.Damaged,3),(PrefixID.Shoddy,3),
                    (PrefixID.Hurtful,10),(PrefixID.Strong,10),(PrefixID.Unpleasant,10),
                    (PrefixID.Weak,3),(PrefixID.Ruthless,10),(PrefixID.Godly,10),
                    (PrefixID.Demonic,10),(PrefixID.Zealous,10),
                    (PrefixID.Quick,10),(PrefixID.Deadly2,10),(PrefixID.Agile,10),
                    (PrefixID.Nimble,10),(PrefixID.Murderous,10),(PrefixID.Slow,3),
                    (PrefixID.Sluggish,3),(PrefixID.Lazy,3),(PrefixID.Annoying,10),(PrefixID.Nasty,10),
                    // 近战通用（无尺寸修正，无 Legendary）
                    (PrefixID.Dangerous,10),(PrefixID.Savage,10),(PrefixID.Sharp,10),
                    (PrefixID.Pointy,10),(PrefixID.Bulky,10),(PrefixID.Heavy,10),
                    (PrefixID.Light,10),(PrefixID.Intimidating,10),(PrefixID.Deadly,10),
                    (PrefixID.Celestial,10),(PrefixID.Furious,10),
                    (PrefixID.Terrible,3),(PrefixID.Dull,3),(PrefixID.Unhappy,3),(PrefixID.Shameful,3),
                    (r,1),
                };
                return WeightedRandomPrefix(rand, isSwing ? swingPool : otherPool);
            }

            return -1; // 其余（Generic、自定义等）走原版逻辑
        }

        // 加权随机选取：按 weight 比例分配概率，O(n) 线性扫描
        private static int WeightedRandomPrefix(UnifiedRandom rand, (int id, int weight)[] pool)
        {
            int total = 0;
            foreach (var (_, w) in pool) total += w;

            int roll = rand.Next(total);
            int cum  = 0;
            foreach (var (id, w) in pool)
            {
                cum += w;
                if (roll < cum) return id;
            }
            return pool[0].id;
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
