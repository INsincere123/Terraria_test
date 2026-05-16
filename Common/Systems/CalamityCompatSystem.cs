using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Prefixes;

namespace TestMod.Common.Systems
{
    public class CalamityCompatSystem : ModSystem
    {
        // ╔══════════════════════════════════════════════════════╗
        // ║           灾厄稀有度调整区域                         ║
        // ║  ApplyRarity  灾厄加载时覆盖的灾厄稀有度内部类名     ║
        // ║  未加载灾厄时物品保持各自 SetDefaults 里的原版稀有度 ║
        // ╚══════════════════════════════════════════════════════╝
        public const string ApplyRarity = "CalamityRed"; // ← 在这里修改

        // ── 运行时稀有度缓存（-2 = 灾厄未加载，不覆盖）────────
        public static int CalamityRarity = -2;

        // ── 灾厄加载状态 ─────────────────────────────────────
        public static bool CalamityLoaded { get; private set; }

        // ── 灾厄真近战伤害类型（反射缓存）───────────────────
        // 灾厄在 SetDefaults 里把 shoot == 0 的近战武器改为此类型
        public static DamageClass CalamityTrueMelee { get; private set; }

        // ── 反射缓存 ─────────────────────────────────────────
        private static FieldInfo _fearmongerSetField;
        private static FieldInfo _gSabatonField;          // CalamityPlayer.gSabaton
        private static Type      _calPlayerType;

        public override void OnModLoad()
        {
            CalamityLoaded = ModLoader.TryGetMod("CalamityMod", out Mod cal);
            if (!CalamityLoaded) return;

            // 缓存稀有度 ID
            if (ModContent.TryFind<ModRarity>("CalamityMod", ApplyRarity, out var rarity))
                CalamityRarity = rarity.Type;

            // 缓存灾厄真近战伤害类型（TrueMeleeDamageClass.Instance）
            // namespace CalamityMod，Instance 是 internal static field
            var trueMeleeType  = cal.Code.GetType("CalamityMod.TrueMeleeDamageClass");
            var instanceField  = trueMeleeType?.GetField("Instance",
                BindingFlags.NonPublic | BindingFlags.Static);
            CalamityTrueMelee = instanceField?.GetValue(null) as DamageClass;

            // 缓存 CalamityPlayer 类型及所需字段
            _calPlayerType      = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
            _fearmongerSetField = _calPlayerType?.GetField("fearmongerSet",
                BindingFlags.Public | BindingFlags.Instance);
            _gSabatonField      = _calPlayerType?.GetField("gSabaton",
                BindingFlags.Public | BindingFlags.Instance);

            // 向灾厄重铸等级表注入"炼化"前缀
            InjectRefinementPrefixTiers(cal);
        }

        // ── 快速下落兼容：设置 gSabaton 标志，激活灾厄 Stompers 加速逻辑 ─
        // 灾厄会在自己的 PostUpdateRunSpeeds 里执行：
        //   maxFallSpeed *= 2（加大上限）+ 如果在上升则 velocity.Y *= 0.7（快速转向下落）
        // 这给出自然加速的手感，无需我们自己管理重力加速度。
        public static void ActivateFastFall(Player player)
        {
            if (!CalamityLoaded || _gSabatonField == null || _calPlayerType == null) return;

            var modPlayers = typeof(Player)
                .GetField("modPlayers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(player) as ModPlayer[];
            if (modPlayers == null) return;

            foreach (var mp in modPlayers)
            {
                if (mp?.GetType() == _calPlayerType)
                {
                    _gSabatonField.SetValue(mp, true);
                    return;
                }
            }
        }

        // ── 套装兼容：免疫跨职业召唤伤害惩罚 ─────────────────
        public static void DisableSummonPenalty(Player player)
        {
            if (!CalamityLoaded || _fearmongerSetField == null) return;

            var modPlayers = typeof(Player)
                .GetField("modPlayers", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(player) as ModPlayer[];

            if (modPlayers == null) return;

            var declaringType = _fearmongerSetField.DeclaringType;
            foreach (var mp in modPlayers)
            {
                if (mp?.GetType() == declaringType)
                {
                    _fearmongerSetField.SetValue(mp, true);
                    return;
                }
            }
        }

        // ── 向灾厄重铸等级表追加"炼化"作为最高 tier ───────────
        // 灾厄的 GetPrefixTier 找不到模组前缀时返回 -1（从 tier 0 重新开始）
        // 在每张表末尾追加新 tier 后，"炼化"就成为该武器类型的终点
        // 工具（ToolPrefixTiers）、泰拉剑（TerrarianPrefixTiers）、盗贼（RoguePrefixTiers）
        // 保持原样，不注入
        private static void InjectRefinementPrefixTiers(Mod cal)
        {
            var reforgeChangeType = cal.Code.GetType("CalamityMod.Prefixes.ReforgeChange");
            if (reforgeChangeType == null)
            {
                ModContent.GetInstance<TestMod>().Logger.Warn("[CalamityCompat] 找不到 ReforgeChange 类，重铸等级注入跳过");
                return;
            }

            // 近战挥动 + 鞭子 → MeleePrefixTiers
            TryAppendTier(reforgeChangeType, "MeleePrefixTiers", new[]
            {
                ModContent.PrefixType<RefinementMeleeSwingPrefix>(),
                ModContent.PrefixType<RefinementWhipPrefix>()
            });

            // 近战非挥动（矛/链球/悠悠球）→ MeleeNoSpeedPrefixTiers
            TryAppendTier(reforgeChangeType, "MeleeNoSpeedPrefixTiers", new[]
            {
                ModContent.PrefixType<RefinementMeleeOtherPrefix>()
            });

            // 同上，满暴击版本
            TryAppendTier(reforgeChangeType, "MeleeNoSpeedAlwaysCritPrefixTiers", new[]
            {
                ModContent.PrefixType<RefinementMeleeOtherPrefix>()
            });

            // 远程（有击退 + 无击退互斥，CanRoll 自动过滤）
            TryAppendTier(reforgeChangeType, "RangedPrefixTiers", new[]
            {
                ModContent.PrefixType<RefinementRangedPrefix>(),
                ModContent.PrefixType<RefinementRangedNoKBPrefix>()
            });

            // 魔法（有击退 + 无击退互斥，CanRoll 自动过滤）
            TryAppendTier(reforgeChangeType, "MagicPrefixTiers", new[]
            {
                ModContent.PrefixType<RefinementMagicPrefix>(),
                ModContent.PrefixType<RefinementMagicNoKBPrefix>()
            });

            // 召唤非鞭（有击退 + 无击退互斥，CanRoll 自动过滤）
            TryAppendTier(reforgeChangeType, "SummonPrefixTiers", new[]
            {
                ModContent.PrefixType<RefinementSummonPrefix>(),
                ModContent.PrefixType<RefinementSummonNoKBPrefix>()
            });
        }

        /// <summary>
        /// 判断某把武器是否为"真近战"：
        /// 灾厄已加载时沿用灾厄的 TrueMeleeDamageClass 标记；
        /// 未加载时回退到 item.shoot == 0 的经典判断。
        /// </summary>
        public static bool IsTrueMeleeWeapon(Item item)
        {
            if (CalamityLoaded && CalamityTrueMelee != null)
                return item.DamageType.CountsAsClass(CalamityTrueMelee);

            return item.DamageType.CountsAsClass(DamageClass.Melee) && item.shoot == ProjectileID.None;
        }

        /// <summary>
        /// 判断某颗弹幕是否为"真近战弹幕"：
        /// 灾厄已加载时直接检查弹幕自身的 DamageType；
        /// 未加载时：
        ///   1. 来源武器 shoot == 0 → 视为真近战
        ///   2. proj.type == weapon.shoot 且 aiStyle 属于真近战延伸名单 → 视为真近战（如挥砍弧光）
        /// </summary>
        public static bool IsTrueMeleeProj(Projectile proj, Item sourceWeapon)
        {
            if (CalamityLoaded && CalamityTrueMelee != null)
                return proj.DamageType.CountsAsClass(CalamityTrueMelee);

            if (!sourceWeapon.DamageType.CountsAsClass(DamageClass.Melee)) return false;

            // 纯挥动武器（无弹幕），来自 Shoot() 覆写的弹幕也视为真近战
            if (sourceWeapon.shoot == ProjectileID.None) return true;

            // 弹幕就是 item.shoot 直接创建，且 aiStyle 属于武器延伸类型
            return proj.type == sourceWeapon.shoot && IsTrueMeleeAIStyle(proj.aiStyle);
        }

        // 真近战武器延伸 aiStyle 名单：
        // 15=链球  19=矛  142=向前刺  152=挥砍弧光(BladeOfGrass/Muramasa)
        // 161=短剑刺  188=暗影之刃弧  190=NightsEdge 挥砍弧光
        private static bool IsTrueMeleeAIStyle(int aiStyle) =>
               aiStyle == ProjAIStyleID.Flail         // 15
            || aiStyle == ProjAIStyleID.Spear         // 19
            || aiStyle == ProjAIStyleID.ForwardStab   // 142
            || aiStyle == ProjAIStyleID.SuperStarBeam // 152
            || aiStyle == ProjAIStyleID.ShortSword    // 161
            || aiStyle == ProjAIStyleID.LightsBane    // 188
            || aiStyle == ProjAIStyleID.NightsEdge;   // 190

        /// <summary>
        /// 向灾厄的某张 int[][] 等级表末尾追加一个新 tier。
        /// 失败时只打 Warn 日志，不抛异常，不影响游戏运行。
        /// </summary>
        private static void TryAppendTier(Type reforgeChangeType, string fieldName, int[] newTier)
        {
            try
            {
                var field = reforgeChangeType.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.Static);

                if (field == null)
                {
                    ModContent.GetInstance<TestMod>().Logger.Warn($"[CalamityCompat] 找不到字段 {fieldName}，跳过注入");
                    return;
                }

                var original = field.GetValue(null) as int[][];
                if (original == null)
                {
                    ModContent.GetInstance<TestMod>().Logger.Warn($"[CalamityCompat] {fieldName} 值为 null，跳过注入");
                    return;
                }

                // 在原数组末尾追加新 tier，生成新数组赋回去
                var extended = new int[original.Length + 1][];
                Array.Copy(original, extended, original.Length);
                extended[original.Length] = newTier;
                field.SetValue(null, extended);
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<TestMod>().Logger.Warn($"[CalamityCompat] 注入 {fieldName} 时出现异常：{ex.Message}");
            }
        }
    }
}