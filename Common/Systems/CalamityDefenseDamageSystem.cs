using System;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    /// <summary>防御损伤兼容；只拦截新增量，不写损伤存量或恢复计时。</summary>
    public class CalamityDefenseDamageSystem : ModSystem
    {
        private const string CalPlayerName = "CalamityMod.CalPlayer.CalamityPlayer";
        internal const string ImmunityTextKey = "Mods.TestMod.DefenseDamageProtection.Immunity";
        internal const string ReductionTextKey = "Mods.TestMod.DefenseDamageProtection.Reduction";
        private static Func<Player, object> immunityCall;
        private static ILHook reductionHook;
        private static Action<string> warn;
        internal static bool ImmunityReady => immunityCall != null;
        internal static bool ReductionReady { get; private set; }

        public override void PostSetupContent()
        {
            Clear();
            warn = message => Mod.Logger.Warn(message);
            if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity)) return;

            try
            {
                InitializeImmunity(calamity);
            }
            catch (Exception ex)
            {
                warn($"[DefenseDamage] 防御损伤免疫兼容停用：{ex.Message}");
            }

            try
            {
                InstallReduction(calamity.Code.GetType(CalPlayerName));
            }
            catch (Exception ex)
            {
                DisableReduction();
                warn($"[DefenseDamage] 防御损伤减半兼容停用：{ex.Message}");
            }
        }

        private static void InitializeImmunity(Mod calamity)
        {
            // 先确认公开协议的玩家/bool入口；实际使用仍走Mod.Call。
            MethodInfo api = calamity.Code.GetType("CalamityMod.ModCalls")?.GetMethod(
                "SetPlayerDefenseDamageImmune", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(Player), typeof(bool) }, null);
            if (api == null || api.ReturnType != typeof(bool))
                throw new MissingMethodException("SetPlayerDefenseDamageImmune(Player, bool)");
            immunityCall = player => calamity.Call("SetPlayerDefenseDamageImmune", player, true);
        }

        internal static void ApplyImmunity(Player player)
        {
            if (!ImmunityReady) return;
            try
            {
                object result = immunityCall(player);
                if (result is Exception error) throw error;
                if (result != null)
                    throw new InvalidOperationException("SetPlayerDefenseDamageImmune返回了非预期结果。");
                // 不调用false；灾厄每帧重置其标志，不能关闭其他装备提供的免疫。
            }
            catch (Exception ex)
            {
                immunityCall = null;
                warn?.Invoke($"[DefenseDamage] 防御损伤免疫调用失败，本轮已停用：{ex.Message}");
            }
        }

        private static void InstallReduction(Type playerType)
        {
            if (playerType == null || !typeof(ModPlayer).IsAssignableFrom(playerType))
                throw new InvalidOperationException("灾厄玩家类型无效。");
            MethodInfo target = playerType.GetMethod("ApplyDefenseDamageInternal",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(int), typeof(bool) }, null);
            if (target == null || target.IsStatic || target.ReturnType != typeof(void) ||
                target.ContainsGenericParameters || target.GetMethodBody() == null)
                throw new MissingMethodException("ApplyDefenseDamageInternal(int, bool)");

            // Apply失败也由调用者释放本轮句柄，不留下局部补丁。
            reductionHook = new ILHook(target, PatchFinalDamage, applyByDefault: false);
            reductionHook.Apply();
            ReductionReady = true;
        }

        private static void PatchFinalDamage(ILContext il)
        {
            // DMD可能把this变为显式首参；两种形式的IL参数1均为新增损伤。
            int offset = il.Method.HasThis ? 0 : 1;
            if (il.Method.Parameters.Count != 2 + offset ||
                offset == 1 && il.Method.Parameters[0].ParameterType.FullName != CalPlayerName ||
                il.Method.Parameters[offset].ParameterType.FullName != "System.Int32" ||
                il.Method.Parameters[offset + 1].ParameterType.FullName != "System.Boolean")
                throw new InvalidOperationException("防御损伤入口IL签名不匹配。");
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Func<ModPlayer, int, int>>(AdjustNewDamage);
            cursor.Emit(OpCodes.Starg, il.Method.Parameters[offset]);
        }

        private static int AdjustNewDamage(ModPlayer calamityPlayer, int damage)
        {
            if (!ReductionReady || damage <= 0) return damage;
            try
            {
                Player player = calamityPlayer.Player;
                return player != null && player.active && !player.dead &&
                    player.TryGetModPlayer(out DefenseDamageProtectionPlayer protection) && protection.Halved
                    ? damage / 2 : damage;
            }
            catch (Exception ex)
            {
                DisableReduction();
                warn?.Invoke($"[DefenseDamage] 防御损伤减半运行失败，本轮已停用：{ex.Message}");
                return damage;
            }
        }

        private static void DisableReduction()
        {
            ReductionReady = false;
            reductionHook?.Dispose();
            reductionHook = null;
        }

        private static void Clear()
        {
            DisableReduction();
            immunityCall = null;
        }

        public override void Unload()
        {
            Clear();
            warn = null;
        }
    }
}
