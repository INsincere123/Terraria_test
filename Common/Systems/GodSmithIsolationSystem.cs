using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using TestMod.Content.Items.Weapons.Plus;
using ProjectileState = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Common.Systems
{
    // 只适配神工开物，不改 CWR 全局模式旗标，不改变普通武器的分发。
    public sealed class GodSmithIsolationSystem : ModSystem
    {
        private const string Root = "CalamityOverhaul.Content.GameModes.GodSmith.";
        private readonly List<IDisposable> hooks = new();
        [ThreadStatic] private static bool isolatedAttack;
        internal bool Ready { get; private set; }

        public override void PostSetupContent()
        {
            Unload();
            if (!ModLoader.TryGetMod("CalamityOverhaul", out Mod overhaul)) return;
            // 旧版本没有该模式，无须安装补丁。
            if (overhaul.Code.GetType(Root + "Framework.GodSmithProjRouter") == null) return;
            try
            {
                Install(overhaul.Code);
                Mod.Logger.Info($"GodSmith isolation ready: CalamityOverhaul {overhaul.Version}, {hooks.Count} hooks.");
            }
            catch (Exception ex)
            {
                Unload();
                Mod.Logger.Warn($"神工开物隔离未能启用（CalamityOverhaul {overhaul.Version}）；+武器可能受到其影响。{ex}");
            }
        }

        // 所有入口共同成功后才就绪；任一安装失败释放本轮全部补丁。
        internal void Install(Assembly assembly)
        {
            Unload();
            try
            {
                GuardOverrides(assembly, "Framework.GodSmithProjRouter", typeof(GlobalProjectile),
                    "OnSpawn", "PreAI", "PostAI", "ModifyHitNPC", "OnHitNPC", "PreDraw", "PostDraw", "OnKill", "OnTileCollide");
                GuardOverrides(assembly, "Core.GodSmithEndowSource", typeof(GlobalProjectile), "OnSpawn");
                GuardOverrides(assembly, "Framework.GodSmithArmorProjMark", typeof(GlobalProjectile), "OnSpawn");
                GuardOverrides(assembly, "Core.GodSmithItem", typeof(GlobalItem),
                    "PostReforge", "ModifyWeaponDamage", "ModifyWeaponCrit", "UseSpeedMultiplier", "UseAnimation", "ModifyTooltips");
                GuardOverrides(assembly, "Framework.GodSmithItemBridge", typeof(GlobalItem), "UseSpeedMultiplier");
                GuardOverrides(assembly, "Framework.GodSmithArmorItemBridge", typeof(GlobalItem),
                    "ModifyWeaponDamage", "UseSpeedMultiplier", "CanConsumeAmmo", "Shoot");
                GuardOverrides(assembly, "Core.GodSmithPlayer", typeof(ModPlayer), "OnHitNPCWithItem", "OnHitNPCWithProj");
                GuardOverrides(assembly, "Framework.GodSmithArmorPlayer", typeof(ModPlayer),
                    "OnHitNPCWithItem", "OnHitNPCWithProj", "ModifyHitNPCWithItem", "ModifyHitNPCWithProj", "OnConsumeMana");
                GuardOverrides(assembly, "Weapons.SummonWhips.GsWhipMarkNPC", typeof(GlobalNPC), "ModifyHitByProjectile");
                GuardOverrides(assembly, "Weapons.MagicMorph.GsMorphNpc", typeof(GlobalNPC), "ModifyHitByProjectile");
                GuardOverrides(assembly, "Weapons.Throwing.GsThrowGlobalNPC", typeof(GlobalNPC), "ModifyIncomingHit");
                GuardOverrides(assembly, "Weapons.Launchers.GsNailGunGlobalNPC", typeof(GlobalNPC), "ModifyIncomingHit");
                InstallAttackScopes();
                // 调度系统会绕过路由直接扫描同 type 实体；仅在其查询中将隔离弹幕视为无匹配类型。
                FilterTypeQueries(assembly, "Weapons.SummonSentries.SentryGrid");
                FilterTypeQueries(assembly, "Weapons.SummonMinions.MinionDoctrine");
                Ready = true;
            }
            catch { Unload(); throw; }
        }

        private void GuardOverrides(Assembly assembly, string name, Type baseType, params string[] names)
        {
            Type type = assembly.GetType(Root + name, true);
            if (!baseType.IsAssignableFrom(type)) throw new InvalidOperationException(type.FullName);
            foreach (string methodName in names)
            {
                MethodInfo contract = baseType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                if (contract == null) throw new MissingMethodException(baseType.FullName, methodName);
                MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    null, contract.GetParameters().Select(p => p.ParameterType).ToArray(), null);
                if (method == null || method.ReturnType != contract.ReturnType || !method.IsVirtual)
                    throw new MissingMethodException(type.FullName, methodName);
                ParameterInfo[] parameters = method.GetParameters();
                int projectile = Array.FindIndex(parameters, p => p.ParameterType == typeof(Projectile));
                int item = Array.FindIndex(parameters, p => p.ParameterType == typeof(Item));
                int source = Array.FindIndex(parameters, p => p.ParameterType == typeof(IEntitySource));
                if (projectile < 0 && item < 0 && method.Name != nameof(GlobalNPC.ModifyIncomingHit))
                    throw new InvalidOperationException(method.ToString());
                Add(method, il => PatchGuard(il, method, projectile, item, source));
            }
        }

        private static void PatchGuard(ILContext il, MethodInfo method, int projectile, int item, int source)
        {
            Type result = method.ReturnType;
            if (result != typeof(void) && result != typeof(bool) && result != typeof(float))
                throw new InvalidOperationException("Unsupported GodSmith hook return: " + method);
            // DMD 可能将 this 展开为显式参数；逻辑参数顺序仍与原实例方法一致。
            int argumentCount = il.Method.Parameters.Count + (il.Method.HasThis ? 1 : 0);
            if (argumentCount != method.GetParameters().Length + 1)
                throw new InvalidOperationException("GodSmith hook signature changed: " + method);
            var cursor = new ILCursor(il);
            ILLabel original = cursor.DefineLabel();
            if (projectile >= 0)
            {
                cursor.Emit(OpCodes.Ldarg, projectile + 1);
                if (source >= 0)
                {
                    cursor.Emit(OpCodes.Ldarg, source + 1);
                    cursor.EmitDelegate<Func<Projectile, IEntitySource, bool>>(ProjectileState.MarkGodSmithIsolation);
                }
                else cursor.EmitDelegate<Func<Projectile, bool>>(ProjectileState.IsGodSmithIsolated);
            }
            else if (item >= 0)
            {
                cursor.Emit(OpCodes.Ldarg, item + 1);
                cursor.EmitDelegate<Func<Item, bool>>(IsIsolatedItem);
            }
            else cursor.EmitDelegate<Func<bool>>(() => isolatedAttack);
            cursor.Emit(OpCodes.Brfalse, original);
            // 默认放行原版流程；只是跳过这个神工开物回调。
            if (result == typeof(bool)) cursor.Emit(OpCodes.Ldc_I4_1);
            if (result == typeof(float)) cursor.Emit(OpCodes.Ldc_R4, 1f);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(original);
        }

        private void FilterTypeQueries(Assembly assembly, string name)
        {
            Type type = assembly.GetType(Root + name, true);
            int patched = 0;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                bool changed = false;
                // 先在操纵器中定位所有原始读取，再原地替换；保留进入该指令的标签。
                var hook = new ILHook(method, il =>
                {
                    var reads = il.Body.Instructions.Where(i => i.MatchLdfld<Projectile>(nameof(Projectile.type))).ToArray();
                    if (reads.Length == 0) return;
                    changed = true;
                    patched++;
                    foreach (var read in reads)
                    {
                        read.OpCode = OpCodes.Nop;
                        read.Operand = null;
                        var cursor = new ILCursor(il);
                        cursor.Goto(read, MoveType.After);
                        cursor.EmitDelegate<Func<Projectile, int>>(VisibleProjectileType);
                    }
                }, false);
                try
                {
                    hook.Apply();
                    if (changed) hooks.Add(hook);
                    else hook.Dispose();
                }
                catch { hook.Dispose(); throw; }
            }
            if (patched == 0) throw new InvalidOperationException("No GodSmith projectile queries found: " + name);
        }

        private void Add(MethodInfo method, ILContext.Manipulator patch)
        {
            var hook = new ILHook(method, patch, false);
            try { hook.Apply(); hooks.Add(hook); }
            catch { hook.Dispose(); throw; }
        }

        internal static bool IsIsolatedItem(Item item) => item?.ModItem is PlusWeapon;
        internal static int VisibleProjectileType(Projectile projectile)
            => ProjectileState.IsGodSmithIsolated(projectile) ? -1 : projectile.type;

        // ModifyIncomingHit 不提供攻击来源；用真实攻击调用栈限定该次结算，嵌套攻击和异常都恢复。
        private void InstallAttackScopes()
        {
            MethodInfo damage = typeof(Projectile).GetMethod(nameof(Projectile.Damage), Type.EmptyTypes);
            if (damage == null || damage.ReturnType != typeof(void)) throw new MissingMethodException("Projectile.Damage");
            hooks.Add(new Hook(damage, (Action<Action<Projectile>, Projectile>)WithProjectileScope));
            MethodInfo melee = typeof(Player).GetMethod("ProcessHitAgainstNPC", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(Item), typeof(Rectangle), typeof(int), typeof(float), typeof(int) }, null);
            if (melee == null || melee.ReturnType != typeof(void)) throw new MissingMethodException("Player.ProcessHitAgainstNPC");
            hooks.Add(new Hook(melee, (Action<Action<Player, Item, Rectangle, int, float, int>, Player, Item, Rectangle, int, float, int>)WithItemScope));
        }

        private static void WithProjectileScope(Action<Projectile> original, Projectile projectile)
        {
            bool previous = isolatedAttack;
            isolatedAttack = ProjectileState.IsGodSmithIsolated(projectile);
            try { original(projectile); }
            finally { isolatedAttack = previous; }
        }

        private static void WithItemScope(Action<Player, Item, Rectangle, int, float, int> original,
            Player player, Item item, Rectangle hitbox, int damage, float knockback, int target)
        {
            bool previous = isolatedAttack;
            isolatedAttack = IsIsolatedItem(item);
            try { original(player, item, hitbox, damage, knockback, target); }
            finally { isolatedAttack = previous; }
        }

        public override void Unload()
        {
            Ready = false;
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            isolatedAttack = false;
        }
    }
}
