using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;
using QuiverProjectile = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Common.Systems
{
    public sealed class VortexQuiverSystem : ModSystem
    {
        private Hook chooseHook, pickHook;
        private ILHook gravityHook;
        internal static bool AmmoReady { get; private set; }
        internal static bool GravityReady { get; private set; }
        private static AmmoSelectionContext currentSelection;

        private sealed class AmmoSelectionContext
        {
            internal Player Player;
            internal Item Weapon;
            internal int Rank = -1;
        }

        private delegate Item ChooseOriginal(Player player, Item weapon);
        private delegate Item ChooseDetour(ChooseOriginal original, Player player, Item weapon);
        private delegate void PickOriginal(Player player, Item weapon, ref int type, ref float speed,
            ref bool canShoot, ref int damage, ref float knockback, out int usedAmmo, bool dontConsume);
        private delegate void PickDetour(PickOriginal original, Player player, Item weapon, ref int type,
            ref float speed, ref bool canShoot, ref int damage, ref float knockback, out int usedAmmo, bool dontConsume);

        public override void Load()
        {
            AmmoReady = GravityReady = false;
            try
            {
                Type byRefInt = typeof(int).MakeByRefType(), byRefFloat = typeof(float).MakeByRefType();
                MethodInfo choose = typeof(Player).GetMethod(nameof(Player.ChooseAmmo), new[] { typeof(Item) });
                MethodInfo pick = typeof(Player).GetMethod(nameof(Player.PickAmmo), BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic, null,
                    new[] { typeof(Item), byRefInt, byRefFloat, typeof(bool).MakeByRefType(), byRefInt,
                        byRefFloat, byRefInt, typeof(bool) }, null);
                if (choose == null || pick == null) throw new MissingMethodException("VortexQuiver ammo entrypoints");
                chooseHook = new Hook(choose, (ChooseDetour)ChooseAmmo);
                pickHook = new Hook(pick, (PickDetour)PickAmmo);
                AmmoReady = true;
            }
            catch (Exception exception)
            {
                chooseHook?.Dispose(); pickHook?.Dispose();
                chooseHook = pickHook = null;
                Mod.Logger.Warn($"[VortexQuiver] 弹药轮换停用：{exception}");
            }
            try
            {
                MethodInfo ai = typeof(Projectile).GetMethod("AI_001", BindingFlags.Instance | BindingFlags.NonPublic);
                if (ai == null) throw new MissingMethodException("Projectile.AI_001");
                gravityHook = new ILHook(ai, ReduceArrowGravity);
                GravityReady = true;
            }
            catch (Exception exception)
            {
                gravityHook?.Dispose(); gravityHook = null;
                Mod.Logger.Warn($"[VortexQuiver] 普通箭低重力停用：{exception}");
            }
        }

        public override void Unload()
        {
            AmmoReady = GravityReady = false;
            chooseHook?.Dispose(); pickHook?.Dispose(); gravityHook?.Dispose();
            chooseHook = pickHook = null; gravityHook = null;
            currentSelection = null;
        }

        public override void OnWorldUnload()
        {
            currentSelection = null;
            if (Main.player == null) return;
            foreach (Player player in Main.player)
                if (player != null && player.TryGetModPlayer(out VortexQuiverPlayer quiver))
                    quiver.ClearSession();
        }

        private static Item ChooseAmmo(ChooseOriginal original, Player player, Item weapon)
        {
            if (AmmoReady && player.active && player.TryGetModPlayer(out VortexQuiverPlayer quiver))
            {
                Item selected = quiver.SelectAmmo(weapon, out int rank);
                if (selected != null)
                {
                    if (currentSelection is { } context && context.Player == player && context.Weapon == weapon)
                        context.Rank = rank;
                    return selected;
                }
            }
            return original(player, weapon);
        }

        private static void PickAmmo(PickOriginal original, Player player, Item weapon, ref int type,
            ref float speed, ref bool canShoot, ref int damage, ref float knockback, out int usedAmmo, bool dontConsume)
        {
            if (!AmmoReady || !player.active || !player.TryGetModPlayer(out VortexQuiverPlayer state) ||
                !state.IsCycling(weapon.useAmmo))
            {
                original(player, weapon, ref type, ref speed, ref canShoot, ref damage,
                    ref knockback, out usedAmmo, dontConsume);
                return;
            }
            int category = weapon.useAmmo;
            AmmoSelectionContext previous = currentSelection;
            var context = new AmmoSelectionContext { Player = player, Weapon = weapon };
            currentSelection = context;
            try
            {
                original(player, weapon, ref type, ref speed, ref canShoot, ref damage,
                    ref knockback, out usedAmmo, dontConsume);
                // dontConsume 是查询/手持载体启动；原版节省弹药与连发免费射击仍以 false 调用。
                if (!dontConsume && canShoot && context.Rank >= 0 &&
                    player.TryGetModPlayer(out VortexQuiverPlayer quiver))
                    quiver.CommitSelection(category, context.Rank);
            }
            finally { currentSelection = previous; }
        }

        private static void ReduceArrowGravity(ILContext il)
        {
            var cursor = new ILCursor(il);
            int count = 0;
            // 只匹配 velocity.Y 的常量重力增量，不修改 Dust、旋转、速度上限或 AI 计时。
            while (cursor.TryGotoNext(MoveType.Before,
                i => i.MatchLdflda(typeof(Entity), nameof(Entity.velocity)),
                i => i.MatchLdflda(typeof(Vector2), nameof(Vector2.Y)),
                i => i.OpCode == OpCodes.Dup,
                i => i.OpCode == OpCodes.Ldind_R4,
                i => i.MatchLdcR4(0.1f),
                i => i.OpCode == OpCodes.Add || i.OpCode == OpCodes.Sub,
                i => i.OpCode == OpCodes.Stind_R4))
            {
                cursor.Index += 5;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, Projectile, float>>((gravity, p) =>
                    p.GetGlobalProjectile<QuiverProjectile>().VortexHasLowGravity(p) ? gravity * 0.2f : gravity);
                count++;
            }
            if (count < 2) throw new InvalidOperationException("找不到普通箭双向重力增量。");
        }
    }
}
