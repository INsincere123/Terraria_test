using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public sealed class HomewardJourneyCompatSystem : ModSystem
    {
        private const int VisibleTimer = 60;
        private static ModPlayer _playerTemplate;
        private static FieldInfo _drawTimerField;
        public static bool TheDarkCompatibilityReady { get; private set; }

        public override void PostSetupContent()
        {
            ClearCompatibility();
            if (!ModLoader.TryGetMod("ContinentOfJourney", out Mod journey)) return;

            try
            {
                var playerType = journey.Code.GetType("ContinentOfJourney.TemplatePlayer");
                var field = playerType?.GetField("theDarkDrawTimer", BindingFlags.Public | BindingFlags.Instance);
                if (field == null || field.FieldType != typeof(int) || field.IsInitOnly)
                    throw new MissingFieldException("ContinentOfJourney.TemplatePlayer", "theDarkDrawTimer");

                foreach (var template in journey.GetContent<ModPlayer>())
                {
                    if (template.GetType() != playerType) continue;
                    _playerTemplate = template;
                    break;
                }
                if (_playerTemplate == null)
                    throw new InvalidOperationException("找不到旅人归途玩家模板。");

                _drawTimerField = field;
                TheDarkCompatibilityReady = true;
            }
            catch (Exception ex)
            {
                ClearCompatibility();
                Mod.Logger.Warn($"[TheDarkImmunity] 暗处遮盖免疫停用：{ex.Message}");
            }
        }

        public static void RemoveTheDarkVeil(Player player)
        {
            if (!TheDarkCompatibilityReady) return;
            try
            {
                // 只缓存模板，每次获取该玩家自己的实例；无需读取或扫描地图物块。
                if (player.TryGetModPlayer(_playerTemplate, out var journeyPlayer))
                    _drawTimerField.SetValue(journeyPlayer, VisibleTimer);
            }
            catch (Exception ex)
            {
                ClearCompatibility();
                ModContent.GetInstance<HomewardJourneyCompatSystem>().Mod.Logger.Warn(
                    $"[TheDarkImmunity] 暗处遮盖免疫停用：{ex.Message}");
            }
        }

        public override void Unload() => ClearCompatibility();

        private static void ClearCompatibility()
        {
            TheDarkCompatibilityReady = false;
            _drawTimerField = null;
            _playerTemplate = null;
        }
    }
}
