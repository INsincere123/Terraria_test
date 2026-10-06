using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;
using TestMod.Content.Items.DamageTypes;
using TestMod.Common.Utilities;
using TestMod.Content.Prefixes;

namespace TestMod.Common.GlobalItems
{
    /// <summary>
    /// 前缀权重池。所有武器类型和饰品的 ChoosePrefix 逻辑集中于此。
    ///
    /// 默认权重规则（具体参数见下方权重调节区）：
    ///   正面/中性词缀              weight = 10
    ///   ReducedNaturalChance 词缀  weight = 3   （原版 66% 拒绝率 → 10×34%≈3）
    ///   自定义炼化/融合（极稀有）   weight = 1   （正面词缀的 1/10）
    /// </summary>
    public partial class GlobalItem
    {
        // ── 权重调节区：每个词缀的相对权重，越大越容易抽中 ──
        private const int WeaponNormalWeight = 40;    // 武器普通词缀
        private const int WeaponReducedWeight = 12;    // 武器 ReducedNaturalChance 词缀
        private const int RefinementWeight = 1;       // 自定义炼化词缀（所有武器类型）
        private const int AccessoryNormalWeight = 40;  // 饰品普通词缀
        private const int FusionWeight = 1;           // 自定义融合词缀
        // 抽中概率 = 该词缀权重 / 当前池的总权重；修改后需 Build + Reload。

        // 未注册的词条没有实例，不能通过 GetInstance/PrefixType 强行取得。
        private static int LoadedPrefixType<T>() where T : ModPrefix =>
            ModContent.TryFind<T>("TestMod", typeof(T).Name, out var prefix) ? prefix.Type : 0;

        private static int RollWithOptionalPrefix(UnifiedRandom rand, (int id, int weight)[] pool,
            int prefix, int weight)
        {
            // 自定义词条仍排在原版候选之后；关闭时不加入候选池。
            return CurveUtils.WeightedRandom(rand, prefix > 0 ? [.. pool, (prefix, weight)] : pool);
        }

        public override int ChoosePrefix(Item item, UnifiedRandom rand)
        {
            // ── 真实伤害武器 ──────────────────────────────────────────
            if (item.DamageType == TrueDamageClass.Instance && item.damage > 0)
            {
                int r = LoadedPrefixType<RefinementPrefix>();
                return RollWithOptionalPrefix(rand, [
                    // 通用词缀（Universal）
                    (PrefixID.Keen,      WeaponNormalWeight), (PrefixID.Superior,  WeaponNormalWeight), (PrefixID.Forceful,  WeaponNormalWeight),
                    (PrefixID.Broken,     WeaponReducedWeight), (PrefixID.Damaged,    WeaponReducedWeight), (PrefixID.Shoddy,     WeaponReducedWeight), // ReducedNaturalChance
                    (PrefixID.Hurtful,   WeaponNormalWeight), (PrefixID.Strong,    WeaponNormalWeight), (PrefixID.Unpleasant,WeaponNormalWeight),
                    (PrefixID.Weak,       WeaponReducedWeight), (PrefixID.Ruthless,  WeaponNormalWeight), (PrefixID.Godly,     WeaponNormalWeight), // Weak=ReducedNaturalChance
                    (PrefixID.Demonic,   WeaponNormalWeight), (PrefixID.Zealous,   WeaponNormalWeight),
                    // 公共词缀（含攻速修正）
                    // Deadly2=43 是通用版；Deadly=20 是近战专属（有尺寸修正），两者不同
                    (PrefixID.Quick,     WeaponNormalWeight), (PrefixID.Deadly2,   WeaponNormalWeight), (PrefixID.Agile,     WeaponNormalWeight),
                    (PrefixID.Nimble,    WeaponNormalWeight), (PrefixID.Murderous, WeaponNormalWeight), (PrefixID.Slow,       WeaponReducedWeight), // ReducedNaturalChance
                    (PrefixID.Sluggish,   WeaponReducedWeight), (PrefixID.Lazy,       WeaponReducedWeight), (PrefixID.Annoying,  WeaponNormalWeight), // ReducedNaturalChance
                    (PrefixID.Nasty,     WeaponNormalWeight),
                ], r, RefinementWeight);
            }

            if (item.damage <= 0) return -1;

            // ── 鞭子（SummonMeleeSpeed）— 先于 Summon/Melee 判断 ──────
            if (item.CountsAsClass(DamageClass.SummonMeleeSpeed))
            {
                int r = LoadedPrefixType<RefinementWhipPrefix>();
                return RollWithOptionalPrefix(rand, [
                    (PrefixID.Keen,WeaponNormalWeight),(PrefixID.Superior,WeaponNormalWeight),(PrefixID.Forceful,WeaponNormalWeight),
                    (PrefixID.Broken,WeaponReducedWeight),(PrefixID.Damaged,WeaponReducedWeight),(PrefixID.Shoddy,WeaponReducedWeight),
                    (PrefixID.Hurtful,WeaponNormalWeight),(PrefixID.Strong,WeaponNormalWeight),(PrefixID.Unpleasant,WeaponNormalWeight),
                    (PrefixID.Weak,WeaponReducedWeight),(PrefixID.Ruthless,WeaponNormalWeight),(PrefixID.Godly,WeaponNormalWeight),
                    (PrefixID.Demonic,WeaponNormalWeight),(PrefixID.Zealous,WeaponNormalWeight),
                    (PrefixID.Quick,WeaponNormalWeight),(PrefixID.Deadly2,WeaponNormalWeight),(PrefixID.Agile,WeaponNormalWeight),
                    (PrefixID.Nimble,WeaponNormalWeight),(PrefixID.Murderous,WeaponNormalWeight),(PrefixID.Slow,WeaponReducedWeight),
                    (PrefixID.Sluggish,WeaponReducedWeight),(PrefixID.Lazy,WeaponReducedWeight),(PrefixID.Annoying,WeaponNormalWeight),(PrefixID.Nasty,WeaponNormalWeight),
                    // 近战系附加
                    (PrefixID.Dangerous,WeaponNormalWeight),(PrefixID.Savage,WeaponNormalWeight),(PrefixID.Sharp,WeaponNormalWeight),
                    (PrefixID.Bulky,WeaponNormalWeight),(PrefixID.Heavy,WeaponNormalWeight),(PrefixID.Light,WeaponNormalWeight),
                    (PrefixID.Celestial,WeaponNormalWeight),(PrefixID.Furious,WeaponNormalWeight),
                ], r, RefinementWeight);
            }

            // ── 召唤（非鞭子）────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Summon))
            {
                bool hasKB = item.knockBack > 0f;
                int r = hasKB
                    ? LoadedPrefixType<RefinementSummonPrefix>()
                    : LoadedPrefixType<RefinementSummonNoKBPrefix>();

                (int id, int weight)[] pool = hasKB
                    ? [
                        (PrefixID.Godly,WeaponNormalWeight),(PrefixID.Demonic,WeaponNormalWeight),(PrefixID.Ruthless,WeaponNormalWeight),
                        (PrefixID.Hurtful,WeaponNormalWeight),(PrefixID.Strong,WeaponNormalWeight),(PrefixID.Keen,WeaponNormalWeight),
                        (PrefixID.Zealous,WeaponNormalWeight),(PrefixID.Broken,WeaponReducedWeight),(PrefixID.Damaged,WeaponReducedWeight),
                        (PrefixID.Weak,WeaponReducedWeight),(PrefixID.Quick,WeaponNormalWeight),(PrefixID.Nimble,WeaponNormalWeight),
                        (PrefixID.Slow,WeaponReducedWeight),(PrefixID.Sluggish,WeaponReducedWeight),
                        (PrefixID.Mythical,WeaponNormalWeight),
                      ]
                    : [
                        (PrefixID.Demonic,WeaponNormalWeight),(PrefixID.Ruthless,WeaponNormalWeight),
                        (PrefixID.Hurtful,WeaponNormalWeight),(PrefixID.Keen,WeaponNormalWeight),
                        (PrefixID.Zealous,WeaponNormalWeight),(PrefixID.Broken,WeaponReducedWeight),(PrefixID.Damaged,WeaponReducedWeight),
                        (PrefixID.Quick,WeaponNormalWeight),(PrefixID.Nimble,WeaponNormalWeight),(PrefixID.Slow,WeaponReducedWeight),
                        (PrefixID.Mythical,WeaponNormalWeight),
                      ];
                return RollWithOptionalPrefix(rand, pool, r, RefinementWeight);
            }

            // ── 魔法 ─────────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Magic) && item.mana > 3)
            {
                bool hasKB = item.knockBack > 0f;
                int r = hasKB
                    ? LoadedPrefixType<RefinementMagicPrefix>()
                    : LoadedPrefixType<RefinementMagicNoKBPrefix>();

                // 魔法专属正面：Mystic(26) Adept(27) Masterful(28) Intense(32) Taboo(33)
                // 魔法负面（ReducedNaturalChance）：Inept(29) Ignorant(30) Deranged(31)
                // 最优：Mythical(83)
                (int id, int weight)[] basePool =
                [
                    (PrefixID.Keen,WeaponNormalWeight),(PrefixID.Superior,WeaponNormalWeight),(PrefixID.Forceful,WeaponNormalWeight),
                    (PrefixID.Broken,WeaponReducedWeight),(PrefixID.Damaged,WeaponReducedWeight),(PrefixID.Shoddy,WeaponReducedWeight),
                    (PrefixID.Hurtful,WeaponNormalWeight),(PrefixID.Unpleasant,WeaponNormalWeight),
                    (PrefixID.Weak,WeaponReducedWeight),(PrefixID.Ruthless,WeaponNormalWeight),(PrefixID.Godly,WeaponNormalWeight),
                    (PrefixID.Demonic,WeaponNormalWeight),(PrefixID.Zealous,WeaponNormalWeight),
                    (PrefixID.Quick,WeaponNormalWeight),(PrefixID.Deadly2,WeaponNormalWeight),(PrefixID.Agile,WeaponNormalWeight),
                    (PrefixID.Nimble,WeaponNormalWeight),(PrefixID.Murderous,WeaponNormalWeight),(PrefixID.Slow,WeaponReducedWeight),
                    (PrefixID.Sluggish,WeaponReducedWeight),(PrefixID.Lazy,WeaponReducedWeight),(PrefixID.Annoying,WeaponNormalWeight),(PrefixID.Nasty,WeaponNormalWeight),
                    (PrefixID.Mystic,WeaponNormalWeight),(PrefixID.Adept,WeaponNormalWeight),(PrefixID.Masterful,WeaponNormalWeight),
                    (PrefixID.Inept,WeaponReducedWeight),(PrefixID.Ignorant,WeaponReducedWeight),(PrefixID.Deranged,WeaponReducedWeight),
                    (PrefixID.Intense,WeaponNormalWeight),(PrefixID.Taboo,WeaponNormalWeight),
                    (PrefixID.Mythical,WeaponNormalWeight),
                ];
                (int id, int weight)[] noKBPool =
                [
                    (PrefixID.Keen,WeaponNormalWeight),(PrefixID.Superior,WeaponNormalWeight),
                    (PrefixID.Broken,WeaponReducedWeight),(PrefixID.Damaged,WeaponReducedWeight),(PrefixID.Shoddy,WeaponReducedWeight),
                    (PrefixID.Hurtful,WeaponNormalWeight),(PrefixID.Unpleasant,WeaponNormalWeight),
                    (PrefixID.Weak,WeaponReducedWeight),(PrefixID.Ruthless,WeaponNormalWeight),(PrefixID.Godly,WeaponNormalWeight),
                    (PrefixID.Demonic,WeaponNormalWeight),(PrefixID.Zealous,WeaponNormalWeight),
                    (PrefixID.Quick,WeaponNormalWeight),(PrefixID.Deadly2,WeaponNormalWeight),(PrefixID.Agile,WeaponNormalWeight),
                    (PrefixID.Nimble,WeaponNormalWeight),(PrefixID.Murderous,WeaponNormalWeight),(PrefixID.Slow,WeaponReducedWeight),
                    (PrefixID.Sluggish,WeaponReducedWeight),(PrefixID.Lazy,WeaponReducedWeight),(PrefixID.Annoying,WeaponNormalWeight),(PrefixID.Nasty,WeaponNormalWeight),
                    (PrefixID.Mystic,WeaponNormalWeight),(PrefixID.Adept,WeaponNormalWeight),(PrefixID.Masterful,WeaponNormalWeight),
                    (PrefixID.Inept,WeaponReducedWeight),(PrefixID.Ignorant,WeaponReducedWeight),(PrefixID.Deranged,WeaponReducedWeight),
                    (PrefixID.Intense,WeaponNormalWeight),(PrefixID.Taboo,WeaponNormalWeight),
                    (PrefixID.Mythical,WeaponNormalWeight),
                ];
                return RollWithOptionalPrefix(rand, hasKB ? basePool : noKBPool, r, RefinementWeight);
            }

            // ── 远程 ─────────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Ranged))
            {
                bool hasKB = item.knockBack > 0f;
                int r = hasKB
                    ? LoadedPrefixType<RefinementRangedPrefix>()
                    : LoadedPrefixType<RefinementRangedNoKBPrefix>();

                // 远程专属正面：Sighted(16) Rapid(17) Hasty(18) Staunch(21) Powerful(25)
                // 远程负面（ReducedNaturalChance）：Awful(22) Lethargic(23) Awkward(24)
                // 最优：Unreal(82)
                (int id, int weight)[] basePool =
                [
                    (PrefixID.Keen,WeaponNormalWeight),(PrefixID.Superior,WeaponNormalWeight),(PrefixID.Forceful,WeaponNormalWeight),
                    (PrefixID.Broken,WeaponReducedWeight),(PrefixID.Damaged,WeaponReducedWeight),(PrefixID.Shoddy,WeaponReducedWeight),
                    (PrefixID.Hurtful,WeaponNormalWeight),(PrefixID.Strong,WeaponNormalWeight),(PrefixID.Unpleasant,WeaponNormalWeight),
                    (PrefixID.Weak,WeaponReducedWeight),(PrefixID.Ruthless,WeaponNormalWeight),(PrefixID.Godly,WeaponNormalWeight),
                    (PrefixID.Demonic,WeaponNormalWeight),(PrefixID.Zealous,WeaponNormalWeight),
                    (PrefixID.Quick,WeaponNormalWeight),(PrefixID.Deadly2,WeaponNormalWeight),(PrefixID.Agile,WeaponNormalWeight),
                    (PrefixID.Nimble,WeaponNormalWeight),(PrefixID.Murderous,WeaponNormalWeight),(PrefixID.Slow,WeaponReducedWeight),
                    (PrefixID.Sluggish,WeaponReducedWeight),(PrefixID.Lazy,WeaponReducedWeight),(PrefixID.Annoying,WeaponNormalWeight),(PrefixID.Nasty,WeaponNormalWeight),
                    (PrefixID.Sighted,WeaponNormalWeight),(PrefixID.Rapid,WeaponNormalWeight),(PrefixID.Hasty,WeaponNormalWeight),
                    (PrefixID.Staunch,WeaponNormalWeight),(PrefixID.Powerful,WeaponNormalWeight),
                    (PrefixID.Awful,WeaponReducedWeight),(PrefixID.Lethargic,WeaponReducedWeight),(PrefixID.Awkward,WeaponReducedWeight),
                    (PrefixID.Unreal,WeaponNormalWeight),
                ];
                (int id, int weight)[] noKBPool =
                [
                    (PrefixID.Keen,WeaponNormalWeight),(PrefixID.Superior,WeaponNormalWeight),
                    (PrefixID.Broken,WeaponReducedWeight),(PrefixID.Damaged,WeaponReducedWeight),(PrefixID.Shoddy,WeaponReducedWeight),
                    (PrefixID.Hurtful,WeaponNormalWeight),(PrefixID.Unpleasant,WeaponNormalWeight),
                    (PrefixID.Weak,WeaponReducedWeight),(PrefixID.Ruthless,WeaponNormalWeight),(PrefixID.Godly,WeaponNormalWeight),
                    (PrefixID.Demonic,WeaponNormalWeight),(PrefixID.Zealous,WeaponNormalWeight),
                    (PrefixID.Quick,WeaponNormalWeight),(PrefixID.Deadly2,WeaponNormalWeight),(PrefixID.Agile,WeaponNormalWeight),
                    (PrefixID.Nimble,WeaponNormalWeight),(PrefixID.Murderous,WeaponNormalWeight),(PrefixID.Slow,WeaponReducedWeight),
                    (PrefixID.Sluggish,WeaponReducedWeight),(PrefixID.Lazy,WeaponReducedWeight),(PrefixID.Annoying,WeaponNormalWeight),(PrefixID.Nasty,WeaponNormalWeight),
                    (PrefixID.Sighted,WeaponNormalWeight),(PrefixID.Rapid,WeaponNormalWeight),(PrefixID.Hasty,WeaponNormalWeight),
                    (PrefixID.Staunch,WeaponNormalWeight),(PrefixID.Powerful,WeaponNormalWeight),
                    (PrefixID.Awful,WeaponReducedWeight),(PrefixID.Lethargic,WeaponReducedWeight),(PrefixID.Awkward,WeaponReducedWeight),
                    (PrefixID.Unreal,WeaponNormalWeight),
                ];
                return RollWithOptionalPrefix(rand, hasKB ? basePool : noKBPool, r, RefinementWeight);
            }

            // ── 近战 ─────────────────────────────────────────────────
            if (item.CountsAsClass(DamageClass.Melee) || item.CountsAsClass(DamageClass.MeleeNoSpeed))
            {
                // noUseGraphic=true → 矛/连枷/悠悠球（Other）；false → 挥砍剑（Swing）
                bool isSwing = !item.noUseGraphic;
                int r = isSwing
                    ? LoadedPrefixType<RefinementMeleeSwingPrefix>()
                    : LoadedPrefixType<RefinementMeleeOtherPrefix>();

                // 近战专属正面：Dangerous(3) Savage(4) Sharp(5) Pointy(6) Bulky(12)
                //              Heavy(14) Light(15) Intimidating(19) Deadly(20) Celestial(34) Furious(35)
                // 近战负面（ReducedNaturalChance）：Tiny(7) Terrible(8) Small(9) Dull(10) Unhappy(11) Shameful(13)
                // 尺寸专属（仅 Swing）：Large(1) Massive(2)；负面尺寸：Tiny(7) Small(9)
                // 最优：Legendary(81)
                (int id, int weight)[] swingPool =
                [
                    (PrefixID.Keen,WeaponNormalWeight),(PrefixID.Superior,WeaponNormalWeight),(PrefixID.Forceful,WeaponNormalWeight),
                    (PrefixID.Broken,WeaponReducedWeight),(PrefixID.Damaged,WeaponReducedWeight),(PrefixID.Shoddy,WeaponReducedWeight),
                    (PrefixID.Hurtful,WeaponNormalWeight),(PrefixID.Strong,WeaponNormalWeight),(PrefixID.Unpleasant,WeaponNormalWeight),
                    (PrefixID.Weak,WeaponReducedWeight),(PrefixID.Ruthless,WeaponNormalWeight),(PrefixID.Godly,WeaponNormalWeight),
                    (PrefixID.Demonic,WeaponNormalWeight),(PrefixID.Zealous,WeaponNormalWeight),
                    (PrefixID.Quick,WeaponNormalWeight),(PrefixID.Deadly2,WeaponNormalWeight),(PrefixID.Agile,WeaponNormalWeight),
                    (PrefixID.Nimble,WeaponNormalWeight),(PrefixID.Murderous,WeaponNormalWeight),(PrefixID.Slow,WeaponReducedWeight),
                    (PrefixID.Sluggish,WeaponReducedWeight),(PrefixID.Lazy,WeaponReducedWeight),(PrefixID.Annoying,WeaponNormalWeight),(PrefixID.Nasty,WeaponNormalWeight),
                    // 近战通用
                    (PrefixID.Dangerous,WeaponNormalWeight),(PrefixID.Savage,WeaponNormalWeight),(PrefixID.Sharp,WeaponNormalWeight),
                    (PrefixID.Pointy,WeaponNormalWeight),(PrefixID.Bulky,WeaponNormalWeight),(PrefixID.Heavy,WeaponNormalWeight),
                    (PrefixID.Light,WeaponNormalWeight),(PrefixID.Intimidating,WeaponNormalWeight),(PrefixID.Deadly,WeaponNormalWeight),
                    (PrefixID.Celestial,WeaponNormalWeight),(PrefixID.Furious,WeaponNormalWeight),
                    (PrefixID.Terrible,WeaponReducedWeight),(PrefixID.Dull,WeaponReducedWeight),(PrefixID.Unhappy,WeaponReducedWeight),(PrefixID.Shameful,WeaponReducedWeight),
                    // 仅 Swing：尺寸修正
                    (PrefixID.Large,WeaponNormalWeight),(PrefixID.Massive,WeaponNormalWeight),
                    (PrefixID.Tiny,WeaponReducedWeight),(PrefixID.Small,WeaponReducedWeight),
                    (PrefixID.Legendary,WeaponNormalWeight),
                ];
                (int id, int weight)[] otherPool =
                [
                    (PrefixID.Keen,WeaponNormalWeight),(PrefixID.Superior,WeaponNormalWeight),(PrefixID.Forceful,WeaponNormalWeight),
                    (PrefixID.Broken,WeaponReducedWeight),(PrefixID.Damaged,WeaponReducedWeight),(PrefixID.Shoddy,WeaponReducedWeight),
                    (PrefixID.Hurtful,WeaponNormalWeight),(PrefixID.Strong,WeaponNormalWeight),(PrefixID.Unpleasant,WeaponNormalWeight),
                    (PrefixID.Weak,WeaponReducedWeight),(PrefixID.Ruthless,WeaponNormalWeight),(PrefixID.Godly,WeaponNormalWeight),
                    (PrefixID.Demonic,WeaponNormalWeight),(PrefixID.Zealous,WeaponNormalWeight),
                    (PrefixID.Quick,WeaponNormalWeight),(PrefixID.Deadly2,WeaponNormalWeight),(PrefixID.Agile,WeaponNormalWeight),
                    (PrefixID.Nimble,WeaponNormalWeight),(PrefixID.Murderous,WeaponNormalWeight),(PrefixID.Slow,WeaponReducedWeight),
                    (PrefixID.Sluggish,WeaponReducedWeight),(PrefixID.Lazy,WeaponReducedWeight),(PrefixID.Annoying,WeaponNormalWeight),(PrefixID.Nasty,WeaponNormalWeight),
                    // 近战通用（无尺寸修正，无 Legendary）
                    (PrefixID.Dangerous,WeaponNormalWeight),(PrefixID.Savage,WeaponNormalWeight),(PrefixID.Sharp,WeaponNormalWeight),
                    (PrefixID.Pointy,WeaponNormalWeight),(PrefixID.Bulky,WeaponNormalWeight),(PrefixID.Heavy,WeaponNormalWeight),
                    (PrefixID.Light,WeaponNormalWeight),(PrefixID.Intimidating,WeaponNormalWeight),(PrefixID.Deadly,WeaponNormalWeight),
                    (PrefixID.Celestial,WeaponNormalWeight),(PrefixID.Furious,WeaponNormalWeight),
                    (PrefixID.Terrible,WeaponReducedWeight),(PrefixID.Dull,WeaponReducedWeight),(PrefixID.Unhappy,WeaponReducedWeight),(PrefixID.Shameful,WeaponReducedWeight),
                ];
                return RollWithOptionalPrefix(rand, isSwing ? swingPool : otherPool, r, RefinementWeight);
            }

            // ── 饰品 ─────────────────────────────────────────────────
            // 原版 20 个饰品词缀共用 AccessoryNormalWeight，融合使用 FusionWeight
            // 融合概率 = FusionWeight / (20 × AccessoryNormalWeight + FusionWeight)，默认约 0.83%
            if (item.accessory)
            {
                int fusion = LoadedPrefixType<FusionPrefix>();
                return RollWithOptionalPrefix(rand,
                [
                    // 防御类
                    (PrefixID.Hard,AccessoryNormalWeight),(PrefixID.Guarding,AccessoryNormalWeight),(PrefixID.Armored,AccessoryNormalWeight),(PrefixID.Warding,AccessoryNormalWeight),
                    // 魔力/暴击
                    (PrefixID.Arcane,AccessoryNormalWeight),(PrefixID.Precise,AccessoryNormalWeight),(PrefixID.Lucky,AccessoryNormalWeight),
                    // 伤害
                    (PrefixID.Jagged,AccessoryNormalWeight),(PrefixID.Spiked,AccessoryNormalWeight),(PrefixID.Angry,AccessoryNormalWeight),(PrefixID.Menacing,AccessoryNormalWeight),
                    // 移速
                    (PrefixID.Brisk,AccessoryNormalWeight),(PrefixID.Fleeting,AccessoryNormalWeight),(PrefixID.Hasty2,AccessoryNormalWeight),(PrefixID.Quick2,AccessoryNormalWeight),
                    // 近战速度
                    (PrefixID.Wild,AccessoryNormalWeight),(PrefixID.Rash,AccessoryNormalWeight),(PrefixID.Intrepid,AccessoryNormalWeight),(PrefixID.Violent,AccessoryNormalWeight),
                    // 最优
                    (PrefixID.Legendary2,AccessoryNormalWeight),
                ], fusion, FusionWeight);
            }

            return -1; // 其余走原版逻辑
        }
    }
}
