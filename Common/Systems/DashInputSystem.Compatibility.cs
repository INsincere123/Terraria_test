using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.Dashes;

namespace TestMod.Common.Systems
{
    public partial class DashInputSystem
    {
        private const BindingFlags InstanceMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private void LoadOptionalDashInputs()
        {
            if (Main.dedServ) return;
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity))
                TryLoadInput("Calamity 水平冲刺", () =>
                {
                    Type type = RequirePlayerType(calamity, "CalamityMod.CalPlayer.CalamityPlayer");
                    Type direction = calamity.Code.GetType("CalamityMod.Enums.DashDirection", true);
                    if (!direction.IsEnum || Enum.GetUnderlyingType(direction) != typeof(int)
                        || Convert.ToInt32(Enum.Parse(direction, "Left")) != -1
                        || Convert.ToInt32(Enum.Parse(direction, "Right")) != 1
                        || Convert.ToInt32(Enum.Parse(direction, "Directionless")) != 0)
                        throw new InvalidOperationException("Calamity DashDirection changed");
                    var method = RequireMethod(type, "HandleHorizontalDash", typeof(bool), direction.MakeByRefType(), typeof(bool));
                    FieldInfo timer = type.GetField("dashTimeMod", InstanceMembers);
                    if (timer?.FieldType != typeof(int)) throw new MissingFieldException(type.FullName, "dashTimeMod");
                    return InstallILInput(method, il => PatchCalamityInput(il, timer));
                });

            if (!ModLoader.TryGetMod("CalamityOverhaul", out Mod overhaul)) return;
            InstallOverhaulInput(overhaul, "CalamityOverhaul.Content.Items.Accessories.NinjaCthulsparkPlayer", "TryStartDash");
            InstallOverhaulInput(overhaul, "CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EyeOfCthulhu.BloodfogIrisPlayer", "TryStartDash");
            InstallOverhaulInput(overhaul, "CalamityOverhaul.Content.Items.Accessories.BrutalRelics.LunaticCultist.RiteRingPlayer", "DetectVeilStep");
        }

        private static Type RequirePlayerType(Mod mod, string name)
        {
            Type type = mod.Code.GetType(name, true);
            if (!typeof(ModPlayer).IsAssignableFrom(type)) throw new InvalidOperationException(name);
            return type;
        }

        private static MethodInfo RequireMethod(Type type, string name, Type result, params Type[] parameters)
        {
            var method = type.GetMethod(name, InstanceMembers | BindingFlags.DeclaredOnly, null, parameters, null);
            if (method == null || method.IsStatic || method.ReturnType != result)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private static void PatchCalamityInput(ILContext il, FieldInfo timer)
        {
            // 模式关闭/远端走原方法；水平方法的 out 枚举已校验为 int 和 -1/0/1。
            var direction = new VariableDefinition(il.Import(typeof(int)));
            il.Body.Variables.Add(direction);
            var cursor = new ILCursor(il);
            ILLabel original = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<ModPlayer, bool>>(p => OwnsHorizontalInput(p.Player));
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_2);
            cursor.EmitDelegate<Func<ModPlayer, bool, int>>((p, force) =>
                p.Player.GetModPlayer<DashPlayer>().TakeCalamityHorizontalRequest(force));
            cursor.Emit(OpCodes.Stloc, direction);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Stfld, timer);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldloc, direction);
            cursor.Emit(OpCodes.Stind_I4);
            cursor.Emit(OpCodes.Ldloc, direction);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Ceq);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Ceq);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(original);
        }

        private void InstallOverhaulInput(Mod mod, string typeName, string methodName)
        {
            TryLoadInput(typeName + "." + methodName, () =>
            {
                Type type = RequirePlayerType(mod, typeName);
                MethodInfo method = RequireMethod(type, methodName, typeof(void));
                return InstallILInput(method, PatchOverhaulInput);
            });
        }

        private static ILHook InstallILInput(MethodInfo method, ILContext.Manipulator patch)
        {
            var hook = new ILHook(method, patch, false);
            try { hook.Apply(); return hook; }
            catch { hook.Dispose(); throw; }
        }

        private readonly record struct HorizontalCondition(Instruction Start, Instruction Body, ILLabel Failure, int Direction);

        private static HorizontalCondition FindHorizontalCondition(ILContext il, string side, int index, int direction)
        {
            var instructions = il.Body.Instructions;
            var fields = instructions.Where(i => i.MatchLdfld<Player>("control" + side)).ToArray();
            if (fields.Length != 1) throw new InvalidOperationException("CWR control" + side + " is not unique");
            int start = instructions.IndexOf(fields[0]) - 2;
            bool PlayerGetter(int at) => instructions[at].MatchLdarg(0)
                && instructions[at + 1].MatchCall<ModPlayer>("get_Player");
            ILLabel FailureAt(int at) => instructions[at].OpCode.FlowControl == FlowControl.Cond_Branch
                && (instructions[at].OpCode == OpCodes.Brfalse || instructions[at].OpCode == OpCodes.Brfalse_S)
                && instructions[at].Operand is ILLabel target ? target
                : throw new InvalidOperationException("CWR horizontal condition changed");

            // 完整验证 control && release && TapWindow；先检查两个方向和消费点，再做任何修改。
            if (start < 0 || start + 12 >= instructions.Count || !PlayerGetter(start) || !PlayerGetter(start + 4)
                || !instructions[start + 6].MatchLdfld<Player>("release" + side))
                throw new InvalidOperationException("CWR horizontal input layout changed");
            ILLabel failure = FailureAt(start + 3);
            if (FailureAt(start + 7).Target != failure.Target || !instructions[start + 8].MatchLdarg(0))
                throw new InvalidOperationException("CWR horizontal short circuit changed");

            int constant = start + 9;
            // RuntimeDetour 的方法副本会把 this 变成显式的第一个参数。
            string owner = il.Method.HasThis ? il.Method.DeclaringType.FullName : il.Method.Parameters[0].ParameterType.FullName;
            // 血雾使用 static TapWindow(Player, int)，另外两个使用实例 TapWindow(int)。
            bool staticWindow = instructions[constant].MatchCall<ModPlayer>("get_Player");
            if (staticWindow) constant++;
            if (!instructions[constant].MatchLdcI4(index)
                || instructions[constant + 1].Operand is not MethodReference tap
                || tap.DeclaringType.FullName != owner || tap.Name != "TapWindow"
                || tap.ReturnType.MetadataType != MetadataType.Boolean || tap.HasThis == staticWindow
                || tap.Parameters.Count != (staticWindow ? 2 : 1)
                || tap.Parameters[^1].ParameterType.MetadataType != MetadataType.Int32
                || (staticWindow && tap.Parameters[0].ParameterType.FullName != typeof(Player).FullName)
                || FailureAt(constant + 2).Target != failure.Target)
                throw new InvalidOperationException("CWR TapWindow signature or branch changed");
            return new(instructions[start], instructions[constant + 3], failure, direction);
        }

        private static void PatchOverhaulInput(ILContext il)
        {
            HorizontalCondition right = FindHorizontalCondition(il, "Right", 2, 1);
            HorizontalCondition left = FindHorizontalCondition(il, "Left", 3, -1);
            var calls = il.Body.Instructions.Where(i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt)
                && i.Operand is MethodReference m && m.DeclaringType.FullName == "CalamityOverhaul.Content.CWRPlayer"
                && m.Name == "TryConsumeRelicDoubleTap" && m.HasThis && m.ReturnType.MetadataType == MetadataType.Boolean
                && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.MetadataType == MetadataType.Int32).ToArray();
            if (calls.Length != 1) throw new InvalidOperationException("CWR dash consumption is not unique");

            // 先装左侧，右侧原有失败标签随左侧入口一起迁移，不能跳过新判定。
            InsertHorizontalCondition(il, left);
            InsertHorizontalCondition(il, right);
            var tapIndex = new VariableDefinition(il.Import(typeof(int)));
            il.Body.Variables.Add(tapIndex);
            var cursor = new ILCursor(il);
            cursor.Goto(calls[0], MoveType.AfterLabel);
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Stloc, tapIndex);
            cursor.Index++;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, tapIndex);
            cursor.EmitDelegate<Func<bool, ModPlayer, int, bool>>((success, p, index) =>
            {
                if (success && (index == 2 || index == 3) && OwnsHorizontalInput(p.Player))
                    p.Player.GetModPlayer<DashPlayer>().ConsumeHorizontalRequest();
                return success;
            });
        }

        private static void InsertHorizontalCondition(ILContext il, HorizontalCondition condition)
        {
            var cursor = new ILCursor(il);
            cursor.Goto(condition.Start, MoveType.AfterLabel);
            ILLabel original = cursor.DefineLabel();
            ILLabel body = il.DefineLabel(condition.Body);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<ModPlayer, bool>>(p => OwnsHorizontalInput(p.Player));
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldc_I4, condition.Direction);
            cursor.EmitDelegate<Func<ModPlayer, int, bool>>((p, dir) => p.Player.GetModPlayer<DashPlayer>().PeekHorizontalRequest() == dir);
            cursor.Emit(OpCodes.Brfalse, condition.Failure);
            cursor.Emit(OpCodes.Br, body);
            cursor.MarkLabel(original);
        }
    }
}
