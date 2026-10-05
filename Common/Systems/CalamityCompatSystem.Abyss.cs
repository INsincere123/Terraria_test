using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    public partial class CalamityCompatSystem
    {
        private const string CalamityPlayerName = "CalamityMod.CalPlayer.CalamityPlayer";
        private const string CalamityUtilsName = "CalamityMod.CalamityUtils";
        private const float AbyssDarknessReduction = 1.5f;
        private const float AbyssGlowBonus = 2f;
        private const float AbyssFlashlightWidthBonus = 0.5f;

        private static PropertyInfo _zoneAbyssProperty;
        private static PropertyInfo _zoneAbyssLayer1Property;
        private static FieldInfo _abyssDarknessField;
        private static FieldInfo _abyssGlowField;
        private static FieldInfo _abyssFlashlightField;
        private static FieldInfo _darknessIntensityField;
        private static FieldInfo _deerclopsPositionField;
        private static FieldInfo _abyssBreathCooldownField;
        private static FieldInfo _abyssDeathField;
        private static FieldInfo _abyssDefenseLossField;
        private static FieldInfo _abyssBreathLossRateField;
        private static FieldInfo _abyssLifeLossField;
        private static ILHook _abyssEffectsHook;
        private static ILHook _abyssLifeRegenHook;
        private static bool _abyssVisionReady;

        private void LoadAbyssCompatibility()
        {
            UnloadAbyssCompatibility();
            if (_calPlayerType == null || _calPlayerTemplate == null)
            {
                Mod.Logger.Warn("[AbyssImmunity] 找不到灾厄玩家模板，深渊兼容停用。");
                return;
            }

            if (!TryLoadAbyssPart("深渊判定", () =>
                _zoneAbyssProperty = RequireAbyssProperty("ZoneAbyss")))
                return;

            TryLoadAbyssPart("深渊照明", () =>
            {
                _abyssDarknessField = RequireAbyssField("abyssDarkness", typeof(float));
                _abyssGlowField = RequireAbyssField("abyssPlayerGlowMultiplier", typeof(float));
                _abyssFlashlightField = RequireAbyssField("abyssFlashlightWidthMultiplier", typeof(float));
                _abyssVisionReady = true;
            });

            TryLoadAbyssPart("深渊黑暗、呼吸与降防", () =>
            {
                _darknessIntensityField = RequireAbyssField("darknessIntensity", typeof(float));
                _deerclopsPositionField = RequireAbyssField("lastDeerclopsPosition", typeof(Vector2?));
                _abyssBreathCooldownField = RequireAbyssField("abyssBreathCD", typeof(int));
                _abyssDeathField = RequireAbyssField("abyssDeath", typeof(bool));
                _abyssDefenseLossField = RequireAbyssField("abyssDefenseLossStat", typeof(int));
                _abyssBreathLossRateField = RequireAbyssField("abyssBreathLossRateStat", typeof(float));
                _abyssLifeLossField = RequireAbyssField("abyssLifeLostAtZeroBreathStat", typeof(int));
                _abyssEffectsHook = new ILHook(RequireAbyssMethod("AbyssEffects"), PatchAbyssEffects);
            });

            TryLoadAbyssPart("深渊离水伤害与第一层环境中毒", () =>
            {
                _zoneAbyssLayer1Property = RequireAbyssProperty("ZoneAbyssLayer1");
                _abyssLifeRegenHook = new ILHook(RequireAbyssMethod("UpdateBadLifeRegen"), PatchAbyssLifeRegen);
            });
        }

        // 只在加载时报告一次；成员或 IL 不匹配不冒险改其他分支，也不阻断模组加载。
        private bool TryLoadAbyssPart(string name, Action load)
        {
            try
            {
                load();
                return true;
            }
            catch (Exception ex)
            {
                Mod.Logger.Warn($"[AbyssImmunity] {name}兼容停用：{ex.Message}");
                return false;
            }
        }

        private static FieldInfo RequireAbyssField(string name, Type type)
        {
            var field = _calPlayerType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != type || field.IsInitOnly)
                throw new MissingFieldException(CalamityPlayerName, name);
            return field;
        }

        private static PropertyInfo RequireAbyssProperty(string name)
        {
            var property = _calPlayerType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property?.PropertyType != typeof(bool) || property.GetMethod == null
                || property.GetMethod.IsStatic || property.GetIndexParameters().Length != 0)
                throw new MissingMemberException(CalamityPlayerName, name);
            return property;
        }

        private static MethodInfo RequireAbyssMethod(string name)
        {
            var method = _calPlayerType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null || method.ReturnType != typeof(void) || method.GetMethodBody() == null)
                throw new MissingMethodException(CalamityPlayerName, name);
            return method;
        }

        private static bool HasAbyssImmunity(Player player) => CalamityLoaded && player.active
            && !player.dead && player.GetModPlayer<AbyssImmunityPlayer>().Enabled;

        private static bool IsInAbyss(ModPlayer calPlayer) =>
            _zoneAbyssProperty != null && (bool)_zoneAbyssProperty.GetValue(calPlayer);

        public static void ApplyAbyssEquipmentEffects(Player player)
        {
            if (!HasAbyssImmunity(player) || _zoneAbyssProperty == null || _calPlayerTemplate == null
                || !player.TryGetModPlayer(_calPlayerTemplate, out var calPlayer))
                return;

            // 原版会逐帧重置 ignoreWater；不用改 wet、位置或生物群系，也不改变翼力。
            player.ignoreWater = true;
            if (!IsInAbyss(calPlayer)) return;

            if (_abyssEffectsHook != null)
                RestoreAbyssBreath(player);

            if (!_abyssVisionReady) return;
            // 独立 ModPlayer 在装备阶段统一执行一次，两个来源不会重复加照明。
            AddAbyssFloat(_abyssDarknessField, calPlayer, -AbyssDarknessReduction);
            AddAbyssFloat(_abyssGlowField, calPlayer, AbyssGlowBonus);
            AddAbyssFloat(_abyssFlashlightField, calPlayer, AbyssFlashlightWidthBonus);
        }

        // 光照扫描每次只判断一次本地玩家，避免逐格读取装备和反射成员。
        internal static bool ShouldIlluminateAbyssViewport()
        {
            if (Main.dedServ || Main.gameMenu || !_abyssVisionReady || _abyssEffectsHook == null
                || _calPlayerTemplate == null || !HasAbyssImmunity(Main.LocalPlayer))
                return false;

            return Main.LocalPlayer.TryGetModPlayer(_calPlayerTemplate, out var calPlayer)
                && IsInAbyss(calPlayer);
        }

        private static void AddAbyssFloat(FieldInfo field, ModPlayer player, float delta) =>
            field.SetValue(player, (float)field.GetValue(player) + delta);

        private static void RestoreAbyssBreath(Player player)
        {
            player.breath = player.breathMax;
            player.breathCD = 0;
        }

        private static bool SkipAbyssEnvironment(ModPlayer calPlayer)
        {
            if (!HasAbyssImmunity(calPlayer.Player) || !IsInAbyss(calPlayer)) return false;

            RestoreAbyssBreath(calPlayer.Player);
            _abyssBreathCooldownField.SetValue(calPlayer, 0);
            _abyssDeathField.SetValue(calPlayer, false);
            _abyssDefenseLossField.SetValue(calPlayer, 0);
            _abyssBreathLossRateField.SetValue(calPlayer, 0f);
            _abyssLifeLossField.SetValue(calPlayer, 0);
            // darknessIntensity 也用于独眼巨鹿的战斗遮罩。其场地仍在绘制时保留该量，
            // 避免逐帧清零让 Boss 的累积黑暗失效；其他减益与后续绘制照常结算。
            if (_deerclopsPositionField.GetValue(calPlayer) == null)
                _darknessIntensityField.SetValue(calPlayer, 0f);
            return true;
        }

        private static bool KeepAbyssDryDamage(bool inAbyss, ModPlayer calPlayer) =>
            inAbyss && !HasAbyssImmunity(calPlayer.Player);

        private static bool KeepAbyssWaterPoison(bool underwater, ModPlayer calPlayer) =>
            underwater && !(HasAbyssImmunity(calPlayer.Player)
                && (bool)_zoneAbyssLayer1Property.GetValue(calPlayer));

        private static bool IsCall(Instruction instruction, string type, string name) =>
            (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
            && instruction.Operand is MethodReference method
            && method.DeclaringType.FullName == type && method.Name == name;

        private static bool IsFalseBranch(Instruction instruction) =>
            instruction.OpCode == OpCodes.Brfalse || instruction.OpCode == OpCodes.Brfalse_S;

        private static bool IsTrueBranch(Instruction instruction) =>
            instruction.OpCode == OpCodes.Brtrue || instruction.OpCode == OpCodes.Brtrue_S;

        private static void PatchAbyssEffects(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before,
                i => i.MatchLdarg(0),
                i => IsCall(i, "Terraria.ModLoader.ModPlayer", "get_Player"),
                i => IsCall(i, CalamityUtilsName, "SetAbyssLightLevels"),
                i => i.MatchLdarg(0),
                i => IsCall(i, CalamityPlayerName, "get_ZoneAbyss"), IsFalseBranch)
                || il.Body.Instructions.Count(i => IsCall(i, CalamityPlayerName, "get_ZoneAbyss")) != 1)
                throw new InvalidOperationException("AbyssEffects 的环境入口 IL 不匹配。");

            // 已执行原有照明汇总，受保护者在环境扣防/扣氧之前返回。
            // 不把 ZoneAbyss 改成 false，避免进入 Signus 的非深渊分支。
            cursor.Index += 3;
            var original = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<ModPlayer, bool>>(SkipAbyssEnvironment);
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(original);
        }

        private static void PatchAbyssLifeRegen(ILContext il)
        {
            var water = new ILCursor(il);
            // 环境中毒是 (ZoneSulphur || ZoneAbyssLayer1) 后的 IsUnderwater 检查。
            // 海灾 ASPoisoning 会直接跳到伤害块，保留该入口，不改变实际区域判定。
            if (!water.TryGotoNext(MoveType.After,
                i => i.MatchLdarg(0),
                i => IsCall(i, CalamityPlayerName, "get_ZoneSulphur"), IsTrueBranch,
                i => i.MatchLdarg(0),
                i => IsCall(i, CalamityPlayerName, "get_ZoneAbyssLayer1"), IsFalseBranch,
                i => i.MatchLdarg(0),
                i => IsCall(i, "Terraria.ModLoader.ModPlayer", "get_Player"),
                i => i.MatchLdfld<Player>(nameof(Player.creativeGodMode)), IsTrueBranch,
                i => i.MatchLdarg(0),
                i => IsCall(i, "Terraria.ModLoader.ModPlayer", "get_Player"),
                i => IsCall(i, CalamityUtilsName, "IsUnderwater")))
                throw new InvalidOperationException("第一层环境中毒 IL 不匹配。");

            var dry = new ILCursor(il);
            if (!dry.TryGotoNext(MoveType.Before,
                i => i.MatchLdarg(0),
                i => IsCall(i, CalamityPlayerName, "get_ZoneAbyss"), IsFalseBranch,
                i => i.MatchLdarg(0),
                i => IsCall(i, "Terraria.ModLoader.ModPlayer", "get_Player"),
                i => IsCall(i, CalamityUtilsName, "IsUnderwater"), IsTrueBranch)
                || il.Body.Instructions.Count(i => IsCall(i, CalamityPlayerName, "get_ZoneSulphur")) != 1
                || il.Body.Instructions.Count(i => IsCall(i, CalamityPlayerName, "get_ZoneAbyss")) != 1)
                throw new InvalidOperationException("深渊离水伤害 IL 不匹配。");

            // 两个位置都确认后才修改，防止版本漂移留下半套补丁。
            dry.Index += 2;
            dry.Emit(OpCodes.Ldarg_0);
            dry.EmitDelegate<Func<bool, ModPlayer, bool>>(KeepAbyssDryDamage);
            water.Emit(OpCodes.Ldarg_0);
            water.EmitDelegate<Func<bool, ModPlayer, bool>>(KeepAbyssWaterPoison);
        }

        private static void UnloadAbyssCompatibility()
        {
            _abyssLifeRegenHook?.Dispose();
            _abyssLifeRegenHook = null;
            _abyssEffectsHook?.Dispose();
            _abyssEffectsHook = null;
            _zoneAbyssProperty = null;
            _zoneAbyssLayer1Property = null;
            _abyssDarknessField = null;
            _abyssGlowField = null;
            _abyssFlashlightField = null;
            _darknessIntensityField = null;
            _deerclopsPositionField = null;
            _abyssBreathCooldownField = null;
            _abyssDeathField = null;
            _abyssDefenseLossField = null;
            _abyssBreathLossRateField = null;
            _abyssLifeLossField = null;
            _abyssVisionReady = false;
        }
    }
}
