using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.Graphics.Light;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    /// <summary>传播完成后补环境亮度，避免整屏补光参与传播或逐次查询。</summary>
    public sealed class AbyssVisionSystem : ModSystem
    {
        private const float AmbientBrightness = 0.5f;
        private static readonly Vector3 AmbientLight = new(AmbientBrightness);
        private static bool _ambientLightEnabled;
        private static int _worldWidth;
        private static int _worldHeight;
        private static FieldInfo _workingLightMapField;
        private static FieldInfo _workingAreaField;
        private ILHook _modernLightingHook;
        private ILHook _legacyLightingHook;

        public override void Load()
        {
            if (Main.dedServ) return;
            try
            {
                _workingLightMapField = RequireField("_workingLightMap", typeof(LightMap));
                _workingAreaField = RequireField("_workingProcessedArea", typeof(Rectangle));
                _modernLightingHook = new ILHook(typeof(LightingEngine).GetMethod("ProcessBlur",
                    BindingFlags.NonPublic | BindingFlags.Instance), PatchModernLighting);
            }
            catch (Exception ex)
            {
                Mod.Logger.Warn($"[AbyssImmunity] 现代光照补光停用：{ex.Message}");
            }

            try
            {
                _legacyLightingHook = new ILHook(typeof(LegacyLighting).GetMethod("CopyFullyProcessedDataOver",
                    BindingFlags.NonPublic | BindingFlags.Instance, null,
                    new[] { typeof(int), typeof(int) }, null), PatchLegacyLighting);
            }
            catch (Exception ex)
            {
                Mod.Logger.Warn($"[AbyssImmunity] 旧式光照补光停用：{ex.Message}");
            }
        }

        private static FieldInfo RequireField(string name, Type type)
        {
            var field = typeof(LightingEngine).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || field.FieldType != type)
                throw new MissingFieldException(typeof(LightingEngine).FullName, name);
            return field;
        }

        public override void Unload()
        {
            _modernLightingHook?.Dispose();
            _modernLightingHook = null;
            _legacyLightingHook?.Dispose();
            _legacyLightingHook = null;
            _workingLightMapField = null;
            _workingAreaField = null;
            ResetVision();
        }

        public override void OnWorldLoad() => ResetVision();
        public override void OnWorldUnload() => ResetVision();

        public override void PostUpdatePlayers()
        {
            // 装备结算后每 tick 只判断一次，不在光照逐格循环中读取玩家或反射深渊属性。
            _ambientLightEnabled = CalamityCompatSystem.ShouldIlluminateAbyssViewport();
            _worldWidth = Main.maxTilesX;
            _worldHeight = Main.maxTilesY;
        }

        private static void ResetVision()
        {
            _ambientLightEnabled = false;
            _worldWidth = 0;
            _worldHeight = 0;
        }

        private static void PatchModernLighting(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (il.Body.Instructions.Count(i => i.MatchCallvirt<LightMap>(nameof(LightMap.Blur))) != 1
                || !cursor.TryGotoNext(MoveType.After, i => i.MatchCallvirt<LightMap>(nameof(LightMap.Blur))))
                throw new InvalidOperationException("现代光照传播结束入口不匹配。");

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<LightingEngine>>(IlluminateProcessedMap);
        }

        private static void IlluminateProcessedMap(LightingEngine engine)
        {
            if (!_ambientLightEnabled) return;
            // 仅在完成一次传播时取两次字段；没有每格反射或 GetColor 委托调用链。
            var map = (LightMap)_workingLightMapField.GetValue(engine);
            var area = (Rectangle)_workingAreaField.GetValue(engine);
            ApplyAmbientLight(area, map, _worldWidth, _worldHeight);
        }

        private static void ApplyAmbientLight(Rectangle area, LightMap map, int worldWidth, int worldHeight)
        {
            int minX = Math.Max(0, 1 - area.X);
            int minY = Math.Max(0, 1 - area.Y);
            int maxX = Math.Min(Math.Min(area.Width, map.Width), worldWidth - 1 - area.X);
            int maxY = Math.Min(Math.Min(area.Height, map.Height), worldHeight - 1 - area.Y);
            for (int x = minX; x < maxX; x++)
            {
                for (int y = minY; y < maxY; y++)
                    map[x, y] = Vector3.Max(map[x, y], AmbientLight);
            }
        }

        private static void PatchLegacyLighting(ILContext il)
        {
            // R2/G2/B2 是传播工作值，R/G/B 是复制给绘制的结果；只补后者。
            const string stateType = "Terraria.Graphics.Light.LegacyLighting/LightingState";
            var stores = il.Body.Instructions.Where(i => i.OpCode == OpCodes.Stfld
                && i.Operand is FieldReference f && f.DeclaringType.FullName == stateType
                && (f.Name == "R" || f.Name == "G" || f.Name == "B")).ToArray();
            if (stores.Length != 6 || stores.Any(i => i.Previous.OpCode != OpCodes.Ldfld
                || i.Previous.Operand is not FieldReference source || source.DeclaringType.FullName != stateType
                || (source.Name != "R2" && source.Name != "G2" && source.Name != "B2")
                || source.FieldType.FullName != "System.Single"))
                throw new InvalidOperationException("旧式光照结果复制入口不匹配。");

            var cursor = new ILCursor(il);
            foreach (var store in stores)
            {
                cursor.Goto(store, MoveType.AfterLabel);
                cursor.EmitDelegate<Func<float, float>>(ApplyAmbientChannel);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApplyAmbientChannel(float value) =>
            _ambientLightEnabled ? Math.Max(value, AmbientBrightness) : value;
    }
}
