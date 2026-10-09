using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ID;
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
        private static bool loggedArrowSpawn, loggedArrowGravity;

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
                Mod.Logger.Info("[VortexQuiver] 已接入普通箭双向重力、圣箭及夜明箭独立重力赋值（20%）。");
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
            loggedArrowSpawn = loggedArrowGravity = false;
        }

        public override void OnWorldUnload()
        {
            currentSelection = null;
            loggedArrowSpawn = loggedArrowGravity = false;
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
            // 先锁定普通分支 ai[0]=15 的赋值，不能仅以整个方法中的 0.1 常量数量判定成功。
            if (!cursor.TryGotoNext(MoveType.After,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(Projectile), nameof(Projectile.ai)),
                i => i.MatchLdcI4(0),
                i => i.MatchLdcR4(15f),
                i => i.OpCode == OpCodes.Stelem_R4,
                i => i.OpCode.Code is Code.Ldloc or Code.Ldloc_S or Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3,
                i => i.OpCode == OpCodes.Brfalse || i.OpCode == OpCodes.Brfalse_S))
                throw new InvalidOperationException("找不到普通箭重力计时分支。");
            int branchStart = cursor.Index;
            var constants = new System.Collections.Generic.List<Instruction>();
            for (int direction = 0; direction < 2; direction++)
            {
                if (!cursor.TryGotoNext(MoveType.Before,
                i => i.MatchLdflda(typeof(Entity), nameof(Entity.velocity)),
                i => i.MatchLdflda(typeof(Vector2), nameof(Vector2.Y)),
                i => i.OpCode == OpCodes.Dup,
                i => i.OpCode == OpCodes.Ldind_R4,
                i => i.MatchLdcR4(0.1f),
                i => i.OpCode == OpCodes.Add || i.OpCode == OpCodes.Sub,
                i => i.OpCode == OpCodes.Stind_R4) || cursor.Index - branchStart > 32 ||
                    il.Body.Instructions[cursor.Index + 5].OpCode != (direction == 0 ? OpCodes.Sub : OpCodes.Add))
                    throw new InvalidOperationException("普通箭双向重力分支不匹配。");
                constants.Add(il.Body.Instructions[cursor.Index + 4]);
                cursor.Index += 7;
            }
            // 圣箭不进入上述普通分支：它在 ai[0]=20 后使用独立的 +0.07 重力。
            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.After,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(Projectile), nameof(Projectile.ai)),
                i => i.MatchLdcI4(0),
                i => i.MatchLdcR4(20f),
                i => i.OpCode == OpCodes.Stelem_R4,
                i => i.MatchLdarg(0),
                i => i.MatchLdflda(typeof(Entity), nameof(Entity.velocity)),
                i => i.MatchLdflda(typeof(Vector2), nameof(Vector2.Y)),
                i => i.OpCode == OpCodes.Dup,
                i => i.OpCode == OpCodes.Ldind_R4,
                i => i.MatchLdcR4(0.07f),
                i => i.OpCode == OpCodes.Add,
                i => i.OpCode == OpCodes.Stind_R4))
                throw new InvalidOperationException("找不到圣箭独立重力分支。");
            constants.Add(il.Body.Instructions[cursor.Index - 3]);
            // 夜明箭按剩余寿命启动重力，ai[] 保存初始速度；不能套用普通箭计时。
            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.After,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(Projectile), nameof(Projectile.type)),
                i => i.MatchLdcI4(ProjectileID.MoonlordArrow),
                i => i.OpCode == OpCodes.Bne_Un || i.OpCode == OpCodes.Bne_Un_S,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(typeof(Projectile), nameof(Projectile.timeLeft)),
                i => i.MatchLdarg(0),
                i => i.MatchCall(typeof(Projectile), "get_MaxUpdates"),
                i => i.MatchLdcI4(45),
                i => i.OpCode == OpCodes.Mul,
                i => i.MatchLdcI4(14),
                i => i.OpCode == OpCodes.Sub,
                i => i.OpCode == OpCodes.Bgt || i.OpCode == OpCodes.Bgt_S,
                i => i.MatchLdarg(0),
                i => i.MatchLdflda(typeof(Entity), nameof(Entity.velocity)),
                i => i.MatchLdflda(typeof(Vector2), nameof(Vector2.Y)),
                i => i.OpCode == OpCodes.Dup,
                i => i.OpCode == OpCodes.Ldind_R4,
                i => i.MatchLdcR4(0.1f),
                i => i.OpCode == OpCodes.Add,
                i => i.OpCode == OpCodes.Stind_R4))
                throw new InvalidOperationException("找不到夜明箭独立重力分支。");
            constants.Add(il.Body.Instructions[cursor.Index - 3]);
            // 完成全部校验后才修改；每次原版实际重力赋值只经过一次倍率修正。
            foreach (Instruction constant in constants)
            {
                cursor.Goto(constant, MoveType.After);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, Projectile, float>>(ScaleArrowGravity);
            }
        }

        internal static void NoteArrowSpawn(Projectile p, bool marked)
        {
            if (loggedArrowSpawn || !p.arrow || p.owner != Main.myPlayer) return;
            loggedArrowSpawn = true;
            ModContent.GetInstance<VortexQuiverSystem>().Mod.Logger.Debug(
                $"[VortexQuiver] 首枚箭来源：type={p.type}, owner={p.owner}, 强化={marked}, GravityReady={GravityReady}");
        }

        private static float ScaleArrowGravity(float gravity, Projectile p)
        {
            bool lowGravity = p.GetGlobalProjectile<QuiverProjectile>().VortexHasLowGravity(p);
            if (!loggedArrowGravity && p.arrow && p.owner == Main.myPlayer)
            {
                loggedArrowGravity = true;
                ModContent.GetInstance<VortexQuiverSystem>().Mod.Logger.Debug(
                    $"[VortexQuiver] 首次箭矢重力：type={p.type}, 强化={lowGravity}, 增量={gravity}->{(lowGravity ? gravity * 0.2f : gravity)}");
            }
            return lowGravity ? gravity * 0.2f : gravity;
        }
    }
}
