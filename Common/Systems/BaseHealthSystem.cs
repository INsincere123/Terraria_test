using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    public sealed class BaseHealthSystem : ModSystem
    {
        private Hook _commandHook;
        private ILHook _statsHook;
        private ILHook _calamityHealthHook;
        internal static bool HealthOverrideReady { get; private set; } = true;

        public override void PostSetupContent()
        {
            if (!ModLoader.TryGetMod("CalamityMod", out Mod calamity)) return;
            try
            {
                InstallCalamityHealthHook(calamity.Code.GetType("CalamityMod.CalPlayer.CalamityPlayer"));
            }
            catch (Exception ex)
            {
                _calamityHealthHook?.Dispose();
                _calamityHealthHook = null;
                HealthOverrideReady = false;
                Mod.Logger.Warn($"[BaseHealth] 灾厄永久生命适配失败，临时生命设置已停用，恢复指令仍可用：{ex.Message}");
            }
        }

        private void InstallCalamityHealthHook(Type playerType)
        {
            var method = playerType?.GetMethod(nameof(ModPlayer.ModifyMaxStats),
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                new[] { typeof(StatModifier).MakeByRefType(), typeof(StatModifier).MakeByRefType() }, null);
            if (method == null || method.ReturnType != typeof(void) || !typeof(ModPlayer).IsAssignableFrom(playerType))
                throw new MissingMethodException("CalamityPlayer.ModifyMaxStats(out StatModifier, out StatModifier)");
            _calamityHealthHook = new ILHook(method, PatchCalamityHealth, applyByDefault: false);
            _calamityHealthHook.Apply();
        }

        private static void PatchCalamityHealth(ILContext il)
        {
            // 安装版2.2.2：四种永久生命道具只汇总至health.Base；mana单独计算。
            // 核对来源及无分支的汇总边界，不清除整个ModifyMaxStats或临时篡改食用记录。
            var instructions = il.Body.Instructions;
            var stores = instructions.Where(i => i.MatchStfld<StatModifier>(nameof(StatModifier.Base))).ToArray();
            if (stores.Length != 2) throw new InvalidOperationException("灾厄生命/魔力基础加成入口不匹配。");
            var healthBlock = instructions.TakeWhile(i => i != stores[0]).ToArray();
            string[] expected = { "sTangerine", "mFruit", "tCloudberry", "sStrawberry" };
            var fields = healthBlock.Where(i => i.OpCode == OpCodes.Ldfld)
                .Select(i => i.Operand as FieldReference).ToArray();
            if (!fields.Select(f => f?.Name).SequenceEqual(expected) ||
                fields.Any(f => f.DeclaringType.FullName != "CalamityMod.CalPlayer.CalamityPlayer" || f.FieldType.FullName != "System.Boolean") ||
                healthBlock.Count(i => i.MatchLdarg(1)) != 2 ||
                healthBlock.Any(i => i.OpCode.FlowControl == FlowControl.Branch || i.OpCode.FlowControl == FlowControl.Cond_Branch) ||
                stores[0].Next == null || !stores[0].Next.MatchLdarg(2))
                throw new InvalidOperationException("灾厄永久生命汇总结构不匹配。");
            var cursor = new ILCursor(il);
            cursor.Goto(stores[0], MoveType.Before);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, ModPlayer, float>>((bonus, calPlayer) =>
                calPlayer.Player.TryGetModPlayer(out BaseHealthPlayer state) && state.BaseHealth > 0 ? 0f : bonus);
        }

        public override void Load()
        {
            try
            {
                // 标准命令只按空格分词，在分发前把 /HP=值 转成 /HP 值。
                var commandMethod = typeof(CommandLoader).GetMethod("HandleCommand",
                    BindingFlags.Static | BindingFlags.NonPublic, null,
                    new[] { typeof(string), typeof(CommandCaller) }, null)
                    ?? throw new MissingMethodException(typeof(CommandLoader).FullName, "HandleCommand");
                _commandHook = new Hook(commandMethod,
                    (Func<Func<string, CommandCaller, bool>, string, CommandCaller, bool>)HandleCommand);
                _statsHook = new ILHook(typeof(PlayerLoader).GetMethod(nameof(PlayerLoader.ModifyMaxStats)), PatchMaxStats);
            }
            catch
            {
                Unload();
                throw;
            }
        }

        private static bool HandleCommand(Func<string, CommandCaller, bool> orig, string input, CommandCaller caller)
        {
            if (caller.CommandType == CommandType.Chat && input.StartsWith("/HP=", StringComparison.OrdinalIgnoreCase))
                input = "/HP " + input.Substring(4).Trim();
            return orig(input, caller);
        }

        private static void PatchMaxStats(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                i => i.MatchCall(typeof(PlayerLoader), nameof(PlayerLoader.ResetMaxStatsToVanilla))))
                throw new InvalidOperationException("基础生命计算入口不匹配。");
            // 只在属性计算中替换原版基础值，随后仍执行全部 ModifyMaxStats。
            // 不钩 ResetMaxStatsToVanilla 本身：角色保存也需要它恢复真实永久成长。
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<Player>>(BaseHealthPlayer.ApplyBaseHealth);
        }

        public override void OnWorldUnload()
        {
            foreach (Player player in Main.player)
                if (player != null && player.TryGetModPlayer(out BaseHealthPlayer state)) state.Clear();
        }

        public override void Unload()
        {
            _commandHook?.Dispose();
            _commandHook = null;
            _statsHook?.Dispose();
            _statsHook = null;
            _calamityHealthHook?.Dispose();
            _calamityHealthHook = null;
            HealthOverrideReady = true;
        }
    }
}
