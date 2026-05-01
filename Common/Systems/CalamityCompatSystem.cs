using System.Reflection;
using Terraria;
using Terraria.ModLoader;

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

        // ── 反射缓存 ─────────────────────────────────────────
        private static FieldInfo _fearmongerSetField;

        public override void OnModLoad()
        {
            CalamityLoaded = ModLoader.TryGetMod("CalamityMod", out Mod cal);
            if (!CalamityLoaded) return;

            // 缓存稀有度 ID
            if (ModContent.TryFind<ModRarity>("CalamityMod", ApplyRarity, out var rarity))
                CalamityRarity = rarity.Type;

            // 缓存 fearmongerSet 反射字段
            var calPlayerType = cal.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer");
            _fearmongerSetField = calPlayerType?.GetField("fearmongerSet",
                BindingFlags.Public | BindingFlags.Instance);
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
    }
}