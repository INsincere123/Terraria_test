using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using EchoProjectile = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Common.Systems
{
    public sealed partial class TimeEchoAttackSystem
    {
        // 作用域覆盖 AI、命中和销毁；嵌套原攻击恢复自己的来源，不继承副本身份。
        [ThreadStatic] private static Projectile worldEffectSource;
        private readonly List<IDisposable> worldEffectHooks = new();
        private delegate void ProjectileActionOriginal(Projectile p);
        private delegate void ProjectileActionDetour(ProjectileActionOriginal original, Projectile p);
        private delegate bool TileSpreadOriginal(int x, int y);
        private delegate bool TileSpreadDetour(TileSpreadOriginal original, int x, int y);

        private static bool IsEchoProjectile(Projectile p) => p != null &&
            ((ReadOnlySpan<Terraria.ModLoader.GlobalProjectile>)p.EntityGlobals).Length > 0 &&
            p.TryGetGlobalProjectile(out EchoProjectile data) && data.IsTimeEchoAttack;
        private static bool IsEchoWorldSource => IsEchoProjectile(worldEffectSource);

        private void LoadWorldEffectHooks()
        {
            // 对签名/IL 的验证失败沿既有 Load 失败路径关闭复制，避免失去隔离后仍运行副本。
            foreach (string name in new[] { nameof(Projectile.Kill), nameof(Projectile.Damage) })
            {
                MethodInfo method = typeof(Projectile).GetMethod(name, Type.EmptyTypes);
                if (method == null) throw new MissingMethodException(name);
                worldEffectHooks.Add(new Hook(method, (ProjectileActionDetour)RunProjectileWorldScope));
            }
            int explosions = 0;
            foreach (MethodInfo method in typeof(Projectile).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (method.Name == nameof(Projectile.ExplodeTiles) && method.ReturnType == typeof(void))
                { worldEffectHooks.Add(new ILHook(method, GuardEchoExplosion)); explosions++; }
            if (explosions == 0) throw new MissingMethodException(nameof(Projectile.ExplodeTiles));
            foreach (string name in new[] { nameof(DelegateMethods.SpreadDry), nameof(DelegateMethods.SpreadWater),
                nameof(DelegateMethods.SpreadLava), nameof(DelegateMethods.SpreadHoney) })
            {
                MethodInfo method = typeof(DelegateMethods).GetMethod(name, new[] { typeof(int), typeof(int) });
                if (method?.ReturnType != typeof(bool)) throw new MissingMethodException(name);
                worldEffectHooks.Add(new Hook(method, (TileSpreadDetour)PreventEchoLiquidSpread));
            }
        }

        private void DisposeWorldEffectHooks()
        {
            for (int i = worldEffectHooks.Count - 1; i >= 0; i--) worldEffectHooks[i].Dispose();
            worldEffectHooks.Clear(); worldEffectSource = null;
        }

        private static void RunProjectileWorldScope(ProjectileActionOriginal original, Projectile p)
        {
            Projectile previous = worldEffectSource; worldEffectSource = p;
            try { original(p); }
            finally { worldEffectSource = previous; }
        }

        private static bool PreventEchoLiquidSpread(TileSpreadOriginal original, int x, int y)
            => !IsEchoWorldSource && original(x, y);

        private static void GuardEchoExplosion(ILContext il)
        {
            var cursor = new ILCursor(il);
            ILLabel proceed = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Projectile, bool>>(p => IsEchoProjectile(p) || IsEchoWorldSource);
            cursor.Emit(OpCodes.Brfalse, proceed);
            cursor.Emit(OpCodes.Ret);
            cursor.MarkLabel(proceed);
        }
    }
}
