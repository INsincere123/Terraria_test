using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Players;
using TestMod.Common.Utilities;
using TestMod.Content.Projectiles.TimeEcho;
using EchoProjectile = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Common.Systems
{
    public sealed partial class TimeEchoAttackSystem : ModSystem
    {
        private Hook shootHook, hitboxHook, bombPlayerHook;
        private ILHook damageHook;
        private Hook projectileAIHook;
        private readonly List<ILHook> ownerHooks = new();
        internal static bool Ready { get; private set; }
        private static ShotContext currentShot;
        private static uint nextRoot;

        private sealed class ShotContext
        {
            internal Player Player;
            internal Item Item;
            internal Projectile Parent;
            internal Vector2 Center, Mouse, Aim;
            internal uint Root, Generation;
            internal readonly List<(Projectile Entity, int Identity)> Candidates = new(16);
        }

        private delegate void ShootOriginal(Player player, int index, Item item, int damage);
        private delegate void ShootDetour(ShootOriginal original, Player player, int index, Item item, int damage);
        private delegate void HitboxOriginal(Item item, Player player, ref Rectangle hitbox, ref bool dontAttack);
        private delegate void HitboxDetour(HitboxOriginal original, Item item, Player player,
            ref Rectangle hitbox, ref bool dontAttack);
        private delegate void BombPlayerOriginal(Projectile projectile, Rectangle hitbox, int player);
        private delegate void BombPlayerDetour(BombPlayerOriginal original, Projectile projectile, Rectangle hitbox, int player);
        private delegate void ProjectileAIOriginal(Projectile p);
        private delegate void ProjectileAIDetour(ProjectileAIOriginal original, Projectile p);

        public override void Load()
        {
            try
            {
                MethodInfo shoot = typeof(Player).GetMethod("ItemCheck_Shoot", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(int), typeof(Item), typeof(int) }, null);
                MethodInfo hitbox = typeof(ItemLoader).GetMethod(nameof(ItemLoader.UseItemHitbox),
                    new[] { typeof(Item), typeof(Player), typeof(Rectangle).MakeByRefType(), typeof(bool).MakeByRefType() });
                MethodInfo bombPlayer = typeof(Projectile).GetMethod("BombsHurtPlayers", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(Rectangle), typeof(int) }, null);
                if (shoot == null || hitbox == null || bombPlayer == null) throw new MissingMethodException("TimeEcho attack entrypoints");
                damageHook = new ILHook(typeof(Projectile).GetMethod(nameof(Projectile.Damage)), IsolateImmunity);
                shootHook = new Hook(shoot, (ShootDetour)CaptureShot);
                hitboxHook = new Hook(hitbox, (HitboxDetour)CaptureMelee);
                bombPlayerHook = new Hook(bombPlayer, (BombPlayerDetour)PreventEchoBombPlayerDamage);
                projectileAIHook = new Hook(typeof(ProjectileLoader).GetMethod(nameof(ProjectileLoader.ProjectileAI)),
                    (ProjectileAIDetour)CaptureCarrierAttack);
                foreach (string name in new[] { nameof(Projectile.VanillaAI), "AI_019_Spears", nameof(Projectile.AI_019_Spears_GetExtensionHitbox),
                    "AI_161_RapierStabs", "AI_075", "AI_182_FinalFractal", "AI_165_Whip", "AI_190_NightsEdge", "AI_191_TrueNightsEdge", "AI_167_SparkleGuitar",
                    nameof(Projectile.FillWhipControlPoints), nameof(Projectile.GetWhipSettings), "CanHitWithMeleeWeapon" })
                {
                    MethodInfo method = typeof(Projectile).GetMethod(name, BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.Public | BindingFlags.NonPublic);
                    if (method == null) throw new MissingMethodException(name);
                    ownerHooks.Add(new ILHook(method, RedirectEchoOwner));
                }
                ownerHooks.Add(new ILHook(typeof(Main).GetMethod(nameof(Main.GetPlayerArmPosition)), RedirectEchoOwner));
                if (!Main.dedServ)
                    foreach (string name in new[] { "DrawProj_Inner", "DrawProj_DrawExtras", "DrawProj_DrawSpecialProjs", "DrawProj_DrawNormalProjs" })
                    {
                        MethodInfo method = typeof(Main).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
                        if (method == null) throw new MissingMethodException(name);
                        ownerHooks.Add(new ILHook(method, RedirectEchoDrawOwner));
                    }
                Ready = true;
            }
            catch (Exception exception)
            {
                DisposeHooks();
                Mod.Logger.Warn("TimeEcho synchronous attacks disabled: attack hooks did not match.", exception);
            }
        }

        public override void Unload()
        {
            DisposeHooks();
            ClearAttackFilters();
            currentShot = null;
            nextRoot = 0;
        }
        public override void OnWorldUnload() { currentShot = null; nextRoot = 0; }
        private void DisposeHooks()
        {
            Ready = false;
            shootHook?.Dispose(); shootHook = null;
            hitboxHook?.Dispose(); hitboxHook = null;
            bombPlayerHook?.Dispose(); bombPlayerHook = null;
            damageHook?.Dispose(); damageHook = null;
            projectileAIHook?.Dispose(); projectileAIHook = null;
            foreach (ILHook hook in ownerHooks) hook.Dispose();
            ownerHooks.Clear();
        }

        private static void PreventEchoBombPlayerDamage(BombPlayerOriginal original, Projectile p, Rectangle hitbox, int player)
        {
            // 原版爆炸玩家伤害不经过 CanHitPlayer/CanHitPvp；只跳过幻影，正常火箭保持原行为。
            if (!p.GetGlobalProjectile<EchoProjectile>().IsTimeEchoAttack) original(p, hitbox, player);
        }

        private static void CaptureShot(ShootOriginal original, Player player, int index, Item item, int damage)
        {
            ShotContext previous = currentShot;
            bool capture = Ready && player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server &&
                player.TryGetModPlayer(out TimeEchoPlayer echo) && echo.CanCopyAttack;
            ShotContext context = null;
            if (capture)
            {
                context = new ShotContext { Player = player, Item = item, Center = player.Center,
                    Mouse = Main.MouseWorld, Aim = (Main.MouseWorld - player.Center).SafeNormalize(new Vector2(player.direction, 0)),
                    Root = ++nextRoot, Generation = player.GetModPlayer<TimeEchoPlayer>().attackState.Generation };
            }
            currentShot = context;
            bool completed = false;
            try { original(player, index, item, damage); completed = true; }
            finally
            {
                currentShot = previous;
                if (completed && context != null) FinishCapture(context, false);
            }
        }

        private static void CaptureCarrierAttack(ProjectileAIOriginal original, Projectile carrier)
        {
            ShotContext previous = currentShot;
            ShotContext context = null;
            if (Ready && Main.netMode != NetmodeID.Server && carrier.owner == Main.myPlayer &&
                !carrier.GetGlobalProjectile<EchoProjectile>().IsTimeEchoAttack)
            {
                Player player = Main.player[carrier.owner];
                // 可复制的攻击本体由副本 AI 自己派生后续攻击，不能再捕获原本体的子弹造成双份。
                if (player.TryGetModPlayer(out TimeEchoPlayer echo) && echo.CanCopyAttack && !CanCopyProjectile(carrier) &&
                    (carrier.aiStyle == ProjAIStyleID.HeldProjectile || player.heldProj == carrier.whoAmI ||
                    carrier.ModProjectile is Content.Projectiles.Ranged.PhantasmHoldout))
                    context = new ShotContext { Player = player, Item = player.HeldItem, Parent = carrier, Center = player.Center,
                        Mouse = Main.MouseWorld, Aim = (Main.MouseWorld - player.Center).SafeNormalize(new Vector2(player.direction, 0)),
                        Root = ++nextRoot, Generation = echo.attackState.Generation };
            }
            currentShot = context;
            bool completed = false;
            try { original(carrier); completed = true; }
            finally
            {
                currentShot = previous;
                // 玩家更新后的射弹 AI 仍在同一 tick；立即投送，不能留到下一 tick 成为过期队列。
                if (completed && context != null) FinishCapture(context, true);
            }
        }

        private static void FinishCapture(ShotContext context, bool flush)
        {
            if (!context.Player.TryGetModPlayer(out TimeEchoPlayer state)) return;
            foreach (var candidate in context.Candidates)
            {
                Projectile p = candidate.Entity;
                if (!p.active || p.identity != candidate.Identity || p.owner != context.Player.whoAmI ||
                    !ReferenceEquals(Main.projectile[p.whoAmI], p) || !CanCopyProjectile(p) ||
                    p.GetGlobalProjectile<EchoProjectile>() is { IsExtraHit: true } or { IsTimeEchoAttack: true }) continue;
                state.QueueShot(TimeEchoShot.Capture(p, context.Player, context.Center, context.Mouse,
                    context.Aim, context.Root, context.Generation));
            }
            if (flush) state.FlushEchoAttacks();
        }

        internal static void ObserveSpawn(Projectile p, IEntitySource source)
        {
            ShotContext context = currentShot;
            if (context == null || context.Candidates.Count >= 64 || p.owner != context.Player.whoAmI ||
                !IsDirectAttackSource(context, source) ||
                !CanCopyProjectile(p) ||
                p.GetGlobalProjectile<EchoProjectile>() is { IsExtraHit: true } or { IsTimeEchoAttack: true }) return;
            context.Candidates.Add((p, p.identity));
        }

        private static bool IsDirectAttackSource(ShotContext context, IEntitySource source)
            => source is EntitySource_ItemUse use && use.Player == context.Player && use.Item == context.Item ||
                context.Parent != null && source is EntitySource_Parent parent && ReferenceEquals(parent.Entity, context.Parent) &&
                source is not IEntitySource_OnHit;

        private static void CaptureMelee(HitboxOriginal original, Item item, Player player,
            ref Rectangle hitbox, ref bool dontAttack)
        {
            original(item, player, ref hitbox, ref dontAttack);
            if (!Ready || Main.netMode == NetmodeID.Server || player.whoAmI != Main.myPlayer || dontAttack ||
                item.noMelee || item.damage <= 0 || item.channel || player.itemAnimation <= 0 || player.heldProj >= 0 ||
                (item.useStyle != ItemUseStyleID.Swing && item.useStyle != ItemUseStyleID.Thrust) ||
                hitbox.Width <= 0 || hitbox.Height <= 0) return;
            if (player.TryGetModPlayer(out TimeEchoPlayer echo)) echo.QueueMelee(hitbox, item);
        }

        internal static void CopyShot(Player player, Vector2 origin, in TimeEchoShot shot)
        {
            if (!shot.Entity.active || shot.Entity.identity != shot.Identity || shot.Entity.owner != player.whoAmI ||
                !ReferenceEquals(Main.projectile[shot.Entity.whoAmI], shot.Entity) || !CanCopyProjectile(shot.Entity)) return;
            TimeEchoAttackGeometry.AimShot(origin, shot.Center, shot.PlayerCenter, shot.Aim, shot.Mouse,
                shot.Velocity, out Vector2 position, out Vector2 velocity);
            var source = new TimeEchoAttackSource(new(shot.Root, shot.Generation), shot.DamageClass, shot.Crit, shot.ArmorPenetration, shot, origin);
            float ai0 = shot.Ai0, ai1 = shot.Ai1;
            if (shot.Entity.aiStyle == ProjAIStyleID.StellarTune) { ai0 = shot.Mouse.X; ai1 = shot.Mouse.Y; }
            int index = Projectile.NewProjectile(source, position, velocity, shot.Type,
                shot.Damage, shot.Knockback, player.whoAmI, ai0, ai1, shot.Ai2);
            if (index >= Main.maxProjectiles) return;
            Main.projectile[index].netUpdate = true;
        }

        internal static int SpawnMelee(Player player, Vector2 origin, in TimeEchoMeleeFrame frame, uint generation)
        {
            var source = new TimeEchoAttackSource(new(++nextRoot, generation), frame.DamageClass, frame.Crit, frame.ArmorPenetration);
            return Projectile.NewProjectile(source, origin, Vector2.Zero, ModContent.ProjectileType<TimeEchoMeleeProjectile>(),
                frame.Damage, frame.Knockback, player.whoAmI);
        }

        // npcProj 仍保留网络 owner，故原版共享免疫数组必须单独隔离。
        // 只替换 NPC.immune[owner] 的读取/写入；不触碰 NPC 数组内容或其他弹幕。
        private static void IsolateImmunity(ILContext il)
        {
            var operations = new List<(Instruction Field, Instruction Operation)>();
            foreach (Instruction instruction in il.Body.Instructions)
            {
                if (!instruction.MatchLdfld<NPC>(nameof(NPC.immune))) continue;
                Instruction current = instruction.Next;
                bool owner = false;
                bool found = false;
                for (int i = 0; current != null && i < 12; i++, current = current.Next)
                {
                    if (current.MatchLdfld<Projectile>(nameof(Projectile.owner))) owner = true;
                    if (current.OpCode == OpCodes.Ldelem_I4 || current.OpCode == OpCodes.Stelem_I4)
                    {
                        if (owner) { operations.Add((instruction, current)); found = true; }
                        break;
                    }
                    if (current.OpCode.FlowControl == FlowControl.Branch || current.OpCode.FlowControl == FlowControl.Cond_Branch) break;
                }
                if (!found) throw new InvalidOperationException("TimeEcho NPC immunity access did not match");
            }
            int reads = 0, writes = 0;
            var cursor = new ILCursor(il);
            var target = new Mono.Cecil.Cil.VariableDefinition(il.Import(typeof(NPC)));
            il.Body.Variables.Add(target);
            foreach (var pair in operations)
            {
                Instruction operation = pair.Operation;
                bool read = operation.OpCode == OpCodes.Ldelem_I4;
                cursor.Goto(pair.Field, MoveType.Before);
                cursor.Emit(OpCodes.Dup);
                cursor.Emit(OpCodes.Stloc, target);
                cursor.Goto(operation, MoveType.Before);
                cursor.Emit(OpCodes.Ldloc, target);
                cursor.Emit(OpCodes.Ldarg_0);
                if (read)
                {
                    cursor.EmitDelegate<Func<int[], int, NPC, Projectile, int>>((array, owner, npc, p) =>
                    {
                        if (!p.GetGlobalProjectile<EchoProjectile>().IsTimeEchoAttack) return array[owner];
                        return owner >= 0 && owner < Main.maxPlayers &&
                            Main.player[owner].TryGetModPlayer(out TimeEchoPlayer echo) ? echo.ReadEchoImmunity(npc) : 0;
                    });
                    reads++;
                }
                else
                {
                    cursor.EmitDelegate<Action<int[], int, int, NPC, Projectile>>((array, owner, value, npc, p) =>
                    {
                        if (!p.GetGlobalProjectile<EchoProjectile>().IsTimeEchoAttack) array[owner] = value;
                        // 共享间隔仍按世界 tick；近战载体的 local 写入不改物品挥砍的共享记录。
                        else if (p.ModProjectile is not TimeEchoMeleeProjectile &&
                            owner >= 0 && owner < Main.maxPlayers &&
                            Main.player[owner].TryGetModPlayer(out TimeEchoPlayer echo)) echo.WriteEchoImmunity(npc, value);
                    });
                    writes++;
                }
                cursor.Remove();
            }
            if (reads == 0 || writes == 0) throw new InvalidOperationException("TimeEcho NPC immunity pattern missing");
            cursor.Index = 0;
            int targetWrites = 0;
            while (cursor.TryGotoNext(MoveType.Before, instruction => instruction.MatchStfld<Player>(nameof(Player.MinionAttackTargetNPC))))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<Player, int, Projectile>>((player, targetIndex, p) =>
                {
                    if (!p.GetGlobalProjectile<EchoProjectile>().IsTimeEchoAttack) player.MinionAttackTargetNPC = targetIndex;
                });
                cursor.Remove();
                targetWrites++;
            }
            if (targetWrites == 0) throw new InvalidOperationException("TimeEcho whip target write did not match");
        }

        private static void RedirectEchoOwner(ILContext il) => RedirectEchoOwnerArgument(il, 0);
        private static void RedirectEchoDrawOwner(ILContext il) => RedirectEchoOwnerArgument(il, 1);
        private static void RedirectEchoOwnerArgument(ILContext il, int projectileArgument)
        {
            var cursor = new ILCursor(il);
            int reads = 0;
            while (cursor.TryGotoNext(MoveType.Before, instruction => instruction.OpCode == OpCodes.Ldelem_Ref &&
                instruction.Previous?.MatchLdfld<Projectile>(nameof(Projectile.owner)) == true))
            {
                cursor.Emit(OpCodes.Ldarg, projectileArgument);
                cursor.EmitDelegate<Func<Player[], int, Projectile, Player>>((players, owner, p) =>
                    p.GetGlobalProjectile<EchoProjectile>().GetEchoOwner(p, players[owner]));
                cursor.Remove();
                reads++;
            }
            if (reads == 0) throw new InvalidOperationException($"TimeEcho owner read did not match: {il.Method.Name}");
        }
    }
}
