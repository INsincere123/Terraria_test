using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Prefixes;

namespace TestMod.Common.Systems
{
    public partial class CalamityCompatSystem : ModSystem
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

        // 内容完成加载后通过公开注册表解析，不依赖灾厄私有 Instance 字段。
        public static DamageClass CalamityTrueMelee { get; private set; }
        private static DamageClass _calamityTrueMeleeNoSpeed;
        private static readonly Dictionary<int, bool> MeleeProjectileOverrides = new();

        // ── 反射缓存 ─────────────────────────────────────────
        private static FieldInfo _fearmongerSetField;
        private static FieldInfo _gSabatonField;          // CalamityPlayer.gSabaton
        private static Type      _calPlayerType;
        private static ModPlayer _calPlayerTemplate;

        public override void OnModLoad()
        {
            CalamityLoaded = ModLoader.TryGetMod("CalamityMod", out Mod cal);
            if (!CalamityLoaded) return;

            // 缓存稀有度 ID
            if (ModContent.TryFind<ModRarity>("CalamityMod", ApplyRarity, out var rarity))
                CalamityRarity = rarity.Type;

            // 缓存 CalamityPlayer 类型及所需字段
            _calPlayerType      = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
            _fearmongerSetField = _calPlayerType?.GetField("fearmongerSet",
                BindingFlags.Public | BindingFlags.Instance);
            _gSabatonField      = _calPlayerType?.GetField("gSabaton",
                BindingFlags.Public | BindingFlags.Instance);

            // 向灾厄重铸等级表注入"炼化"前缀
            InjectRefinementPrefixTiers(cal);
        }

        public override void PostSetupContent()
        {
            ClearMeleeCompatibility();
            if (!CalamityLoaded) return;

            // 只在内容加载完成时解析一次模板；运行时通过其 Index 获取各玩家自己的实例。
            // 不缓存玩家实例，避免切世界、玩家替换或克隆后引用旧的 ModPlayer。
            if (ModLoader.TryGetMod("CalamityMod", out Mod cal))
            {
                foreach (var modPlayer in cal.GetContent<ModPlayer>())
                {
                    if (modPlayer.GetType() == _calPlayerType)
                    {
                        _calPlayerTemplate = modPlayer;
                        break;
                    }
                }
            }

            LoadAbyssCompatibility();

            ModContent.TryFind<DamageClass>("CalamityMod", "TrueMeleeDamageClass", out var trueMelee);
            ModContent.TryFind<DamageClass>("CalamityMod", "TrueMeleeNoSpeedDamageClass", out var trueMeleeNoSpeed);
            CalamityTrueMelee = trueMelee;
            _calamityTrueMeleeNoSpeed = trueMeleeNoSpeed;

            // Terratomere 的剑身始终贴着玩家；随后释放的残留斩击、剑气不继承资格。
            RegisterMeleeProjectile("TerratomereHoldoutProj", true);
            RegisterMeleeProjectile("TerratomereMeleeSlash", false);
            RegisterMeleeProjectile("TerratomereSwordBeam", false);
            RegisterMeleeProjectile("TerratomereSlash", false);

            if (trueMelee == null || trueMeleeNoSpeed == null)
                Mod.Logger.Warn("[BerserkBlade] 灾厄真近战类型未完整解析；已识别弹幕规则仍可用，其余近战弹幕降效处理。");
        }

        private void RegisterMeleeProjectile(string name, bool direct)
        {
            if (ModContent.TryFind<ModProjectile>("CalamityMod", name, out var projectile))
                MeleeProjectileOverrides[projectile.Type] = direct;
            else
                Mod.Logger.Warn($"[BerserkBlade] 找不到 CalamityMod/{name}，跳过该弹幕规则。");
        }

        public static bool TryGetMeleeProjectileOverride(Projectile projectile, out bool direct) =>
            MeleeProjectileOverrides.TryGetValue(projectile.type, out direct);

        private static void ClearMeleeCompatibility()
        {
            CalamityTrueMelee = null;
            _calamityTrueMeleeNoSpeed = null;
            MeleeProjectileOverrides.Clear();
        }

        public override void Unload()
        {
            UnloadAbyssCompatibility();
            ClearMeleeCompatibility();
            _calPlayerTemplate = null;
            _calPlayerType = null;
            _gSabatonField = null;
            _fearmongerSetField = null;
            CalamityLoaded = false;
            CalamityRarity = -2;
        }

        // ── 快速下落兼容：设置 gSabaton 标志，激活灾厄 Stompers 加速逻辑 ─
        // 灾厄会在自己的 PostUpdateRunSpeeds 里执行：
        //   maxFallSpeed *= 2（加大上限）+ 如果在上升则 velocity.Y *= 0.7（快速转向下落）
        // 这给出自然加速的手感，无需我们自己管理重力加速度。
        public static void ActivateFastFall(Player player)
        {
            if (!CalamityLoaded || _gSabatonField == null) return;

            if (player.TryGetModPlayer(_calPlayerTemplate, out var modPlayer))
                _gSabatonField.SetValue(modPlayer, true);
        }

        // ── 套装兼容：免疫跨职业召唤伤害惩罚 ─────────────────
        public static void DisableSummonPenalty(Player player)
        {
            if (!CalamityLoaded || _fearmongerSetField == null) return;

            if (player.TryGetModPlayer(_calPlayerTemplate, out var modPlayer))
                _fearmongerSetField.SetValue(modPlayer, true);
        }

        // ── 向灾厄重铸等级表追加"炼化"作为最高 tier ───────────
        // 灾厄的 GetPrefixTier 找不到模组前缀时返回 -1（从 tier 0 重新开始）
        // 在每张表末尾追加新 tier 后，"炼化"就成为该武器类型的终点
        // 工具（ToolPrefixTiers）、泰拉剑（TerrarianPrefixTiers）、盗贼（RoguePrefixTiers）
        // 保持原样，不注入
        private static void InjectRefinementPrefixTiers(Mod cal)
        {
            // 关闭时炼化内容没有注册，也不向灾厄注入相应等级。
            if (!PrefixAvailabilitySystem.RefinementEnabled)
                return;
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

        /// <summary>仅检查灾厄明确的真近战标记；通用命中分类由 MeleeHitClassifier 负责。</summary>
        public static bool IsTrueMeleeProj(Projectile proj) =>
            CalamityLoaded
            && ((CalamityTrueMelee != null && proj.DamageType.CountsAsClass(CalamityTrueMelee))
                || (_calamityTrueMeleeNoSpeed != null && proj.DamageType.CountsAsClass(_calamityTrueMeleeNoSpeed)));

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
