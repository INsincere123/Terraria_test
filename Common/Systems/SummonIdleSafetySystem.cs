using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    // 只修复原版队列位置计算的零数量边界，不接管普通仆从的索敌、攻击或移动 AI。
    public sealed class SummonIdleSafetySystem : ModSystem
    {
        private delegate void IdleOriginal(Projectile projectile, int index, int total,
            out Vector2 position, out float rotation);
        private delegate void IdleDetour(IdleOriginal original, Projectile projectile, int index, int total,
            out Vector2 position, out float rotation);
        private Hook idleHook;
        private readonly HashSet<string> warnings = new();

        public override void Load()
        {
            MethodInfo method = typeof(Projectile).GetMethod("AI_156_GetIdlePosition",
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(int), typeof(int), typeof(Vector2).MakeByRefType(), typeof(float).MakeByRefType() }, null);
            try
            {
                if (method?.ReturnType != typeof(void)) throw new MissingMethodException("Projectile.AI_156_GetIdlePosition");
                idleHook = new Hook(method, (IdleDetour)GetIdlePosition, false);
                idleHook.Apply();
            }
            catch (Exception exception)
            {
                idleHook?.Dispose(); idleHook = null;
                Mod.Logger.Warn("[SummonIdle] 原版仆从队列零数量保护未能安装。", exception);
            }
        }

        private void GetIdlePosition(IdleOriginal original, Projectile p, int index, int total,
            out Vector2 position, out float rotation)
        {
            if (total <= 0)
            {
                // 正常世界中的活动实例至少会统计到自己；保留异常状态帮助定位清理/替换路径。
                bool registered = p.whoAmI >= 0 && p.whoAmI < Main.maxProjectiles &&
                    ReferenceEquals(Main.projectile[p.whoAmI], p);
                string content = p.ModProjectile?.FullName ?? "Terraria/Projectile:" + p.type;
                string key = content + ":" + p.active + ":" + registered;
                if (warnings.Add(key))
                    Mod.Logger.Warn($"[SummonIdle] 队列数量为{total}：{content}, slot={p.whoAmI}, owner={p.owner}, " +
                        $"active={p.active}, registered={registered}, index={index}；本次采用安全待机位置。");
                if (!p.active)
                {
                    position = p.Center;
                    rotation = p.rotation;
                    return;
                }
                // 活动但未被本次扫描计入的实例按单体计算，避免取模零；正常队列原样转发。
                index = 0;
                total = 1;
            }
            original(p, index, total, out position, out rotation);
        }

        public override void OnWorldUnload() => warnings.Clear();
        public override void Unload()
        {
            idleHook?.Dispose(); idleHook = null;
            warnings.Clear();
        }
    }
}
