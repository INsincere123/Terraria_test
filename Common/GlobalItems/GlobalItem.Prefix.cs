using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using TestMod.Items.DamageTypes;
using TestMod.Common.Utilities;

namespace TestMod.Common.GlobalItems
{
    /// <summary>
    /// 前缀权重池。所有武器类型和饰品的 ChoosePrefix 逻辑集中于此。
    ///
    /// 权重规则（还原原版行为）：
    ///   正面/中性词缀              weight = 10
    ///   ReducedNaturalChance 词缀  weight = 3   （原版 66% 拒绝率 → 10×34%≈3）
    ///   自定义炼化/融合（极稀有）   weight = 1   （正面词缀的 1/10）
    /// </summary>
    public partial class GlobalItem
    {
        public override int ChoosePrefix(Item item, UnifiedRandom rand)
        {
            // ── 真实伤害武器 ──────────────────────────────────────────
            if (item.DamageType == TrueDamageClass.Instance && item.damage > 0)
            {
                int r = ModContent.GetInstance<Prefixes.RefinementPrefix>().Type;
                return CurveUtils.WeightedRandom(rand, [
                    // 通用词缀（Universal）
                    (PrefixID.Keen,      10), (PrefixID.Superior,  10), (PrefixID.Forceful,  10),
                    (PrefixID.Broken,     3), (PrefixID.Damaged,    3), (PrefixID.Shoddy,     3), // ReducedNaturalChance
                    (PrefixID.Hurtful,   10), (PrefixID.Strong,    10), (PrefixID.Unpleasant,10),
                    (PrefixID.Weak,       3), (PrefixID.Ruthless,  10), (PrefixID.Godly,     10), // Weak=ReducedNaturalChance
                    (PrefixID.Demonic,   10), (PrefixID.Zealous,   10),
                    // 公共词缀（含攻速修正）
                    // Deadly2=43 是通用版；Deadly=20 是近战专属（有尺寸修正），两者不同
                    (PrefixID.Quick,     10), (PrefixID.Deadly2,   10), (PrefixID.Agile,     10),
                    (PrefixID.Nimble,    10), (PrefixID.Murderous, 10), (PrefixID.Slow,       3), // ReducedNaturalChance
                    (PrefixID.Sluggish,   3), (PrefixID.Lazy,       3), (PrefixID.Annoying,  10), // ReducedNaturalChance
                    (PrefixID.Nasty,     10),
                    // 炼化（极稀有）
                    (r, 1),
                ]);
            }

            if (item.damage <= 0) return -1;

            // ── 鞭子（SummonMeleeSpeed）— 先于 Summon/Melee 判断 ──────
            if (item.CountsAsClass(DamageClass.SummonMeleeSpeed))
            {
                int r = ModContent.GetInstance<Prefixes.RefinementWhipPrefix>().Type;
                return CurveUtils.WeightedRandom(rand, [
                    (PrefixID.Keen,10),(PrefixID.Superior,10),(PrefixID.Forceful,10),
                    (PrefixID.Broken,3),(PrefixID.Damaged,3),(PrefixID.Shoddy,3),
                    (PrefixID.Hurtful,10),(PrefixID.Strong,10),(PrefixID.Unpleasant,10),
                    (PrefixID.Weak,3),(PrefixID.Ruthless,10),(PrefixID.Godly,10),
                    (PrefixID.Demonic,10),(PrefixID.Zealous,10),
                    (PrefixID.Quick,10),(PrefixID.Deadly2,10),(PrefixID.Agile,10),
                    (PrefixID.Nimble,10),(PrefixID.Murderous,10),(PrefixID.Slow,3),
                    (PrefixID.Sluggish,3),(PrefixID.Lazy,3),(PrefixID.Annoying,10),(PrefixID.Nasty,10),
                    // 近战系附加
                    (PrefixID.Dangerous,10),(PrefixID.Savage,10),(PrefixID.Sharp,10),
                    (PrefixID.Bulky,10),(PrefixID.Heavy,10),(PrefixID.Light,10),
                    (PrefixID.Celestial,10),(PrefixID.Furious,10),
                    (r, 1),
                ]);
            }

            // ── 召唤（非鞭子）────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Summon))
            {
                bool hasKB = item.knockBack > 0f;
                int r = hasKB
                    ? ModContent.GetInstance<Prefixes.RefinementSummonPrefix>().Type
                    : ModContent.GetInstance<Prefixes.RefinementSummonNoKBPrefix>().Type;

                (int id, int weight)[] pool = hasKB
                    ? [
                        (PrefixID.Godly,10),(PrefixID.Demonic,10),(PrefixID.Ruthless,10),
                        (PrefixID.Hurtful,10),(PrefixID.Strong,10),(PrefixID.Keen,10),
                        (PrefixID.Zealous,10),(PrefixID.Broken,3),(PrefixID.Damaged,3),
                        (PrefixID.Weak,3),(PrefixID.Quick,10),(PrefixID.Nimble,10),
                        (PrefixID.Slow,3),(PrefixID.Sluggish,3),
                        (PrefixID.Mythical,10),
                        (r, 1),
                      ]
                    : [
                        (PrefixID.Demonic,10),(PrefixID.Ruthless,10),
                        (PrefixID.Hurtful,10),(PrefixID.Keen,10),
                        (PrefixID.Zealous,10),(PrefixID.Broken,3),(PrefixID.Damaged,3),
                        (PrefixID.Quick,10),(PrefixID.Nimble,10),(PrefixID.Slow,3),
                        (PrefixID.Mythical,10),
                        (r, 1),
                      ];
                return CurveUtils.WeightedRandom(rand, pool);
            }

            // ── 魔法 ─────────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Magic) && item.mana > 3)
            {
                bool hasKB = item.knockBack > 0f;
                int r = hasKB
                    ? ModContent.GetInstance<Prefixes.RefinementMagicPrefix>().Type
                    : ModContent.GetInstance<Prefixes.RefinementMagicNoKBPrefix>().Type;

                // 魔法专属正面：Mystic(26) Adept(27) Masterful(28) Intense(32) Taboo(33)
                // 魔法负面（ReducedNaturalChance）：Inept(29) Ignorant(30) Deranged(31)
                // 最优：Mythical(83)
                (int id, int weight)[] basePool =
                [
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
                    (r, 1),
                ];
                (int id, int weight)[] noKBPool =
                [
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
                    (r, 1),
                ];
                return CurveUtils.WeightedRandom(rand, hasKB ? basePool : noKBPool);
            }

            // ── 远程 ─────────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Ranged))
            {
                bool hasKB = item.knockBack > 0f;
                int r = hasKB
                    ? ModContent.GetInstance<Prefixes.RefinementRangedPrefix>().Type
                    : ModContent.GetInstance<Prefixes.RefinementRangedNoKBPrefix>().Type;

                // 远程专属正面：Sighted(16) Rapid(17) Hasty(18) Staunch(21) Powerful(25)
                // 远程负面（ReducedNaturalChance）：Awful(22) Lethargic(23) Awkward(24)
                // 最优：Unreal(82)
                (int id, int weight)[] basePool =
                [
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
                    (r, 1),
                ];
                (int id, int weight)[] noKBPool =
                [
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
                    (r, 1),
                ];
                return CurveUtils.WeightedRandom(rand, hasKB ? basePool : noKBPool);
            }

            // ── 近战 ─────────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Melee) || item.CountsAsClass(DamageClass.MeleeNoSpeed))
            {
                // noUseGraphic=true → 矛/连枷/悠悠球（Other）；false → 挥砍剑（Swing）
                bool isSwing = !item.noUseGraphic;
                int r = isSwing
                    ? ModContent.GetInstance<Prefixes.RefinementMeleeSwingPrefix>().Type
                    : ModContent.GetInstance<Prefixes.RefinementMeleeOtherPrefix>().Type;

                // 近战专属正面：Dangerous(3) Savage(4) Sharp(5) Pointy(6) Bulky(12)
                //              Heavy(14) Light(15) Intimidating(19) Deadly(20) Celestial(34) Furious(35)
                // 近战负面（ReducedNaturalChance）：Tiny(7) Terrible(8) Small(9) Dull(10) Unhappy(11) Shameful(13)
                // 尺寸专属（仅 Swing）：Large(1) Massive(2)；负面尺寸：Tiny(7) Small(9)
                // 最优：Legendary(81)
                (int id, int weight)[] swingPool =
                [
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
                    (r, 1),
                ];
                (int id, int weight)[] otherPool =
                [
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
                    (r, 1),
                ];
                return CurveUtils.WeightedRandom(rand, isSwing ? swingPool : otherPool);
            }

            // ── 饰品 ─────────────────────────────────────────────────
            // 原版 20 个饰品词缀全部平等（weight=6），融合（自定义）weight=1
            // 融合概率 = 1 / (20×6+1) = 1/121 ≈ 0.83%，约为普通词缀的 1/6
            if (item.accessory)
            {
                int fusion = ModContent.GetInstance<Prefixes.FusionPrefix>().Type;
                return CurveUtils.WeightedRandom(rand,
                [
                    // 防御类
                    (PrefixID.Hard,6),(PrefixID.Guarding,6),(PrefixID.Armored,6),(PrefixID.Warding,6),
                    // 魔力/暴击
                    (PrefixID.Arcane,6),(PrefixID.Precise,6),(PrefixID.Lucky,6),
                    // 伤害
                    (PrefixID.Jagged,6),(PrefixID.Spiked,6),(PrefixID.Angry,6),(PrefixID.Menacing,6),
                    // 移速
                    (PrefixID.Brisk,6),(PrefixID.Fleeting,6),(PrefixID.Hasty2,6),(PrefixID.Quick2,6),
                    // 近战速度
                    (PrefixID.Wild,6),(PrefixID.Rash,6),(PrefixID.Intrepid,6),(PrefixID.Violent,6),
                    // 最优
                    (PrefixID.Legendary2,6),
                    // 融合（极稀有）
                    (fusion, 1),
                ]);
            }

            return -1; // 其余走原版逻辑
        }
    }
}
