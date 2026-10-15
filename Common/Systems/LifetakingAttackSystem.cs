using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.GlobalNPCs;
using TestMod.Common.GlobalProjectiles;
using TestMod.Common.Mechanics.Lifetaking;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    public sealed partial class LifetakingAttackSystem : ModSystem
    {
        private readonly List<IDisposable> hooks = new();
        internal static bool Ready { get; private set; }
        private static VirtualHit current;
        private static Player meleeSource;
        private static Action<Player, Item, Rectangle, int, float, int> processMelee;
        private static readonly HashSet<int> FailedProjectileTypes = new();
        private static bool meleeFailed;

        private sealed class VirtualHit
        {
            internal Projectile Projectile;
            internal Player Player;
            internal Item Item;
            internal NPC Target;
            internal uint Generation;
            internal bool Entered, GeometryConsumed;
        }

        private delegate bool CollisionOriginal(Projectile p, Rectangle own, Rectangle target);
        private delegate bool CollisionDetour(CollisionOriginal original, Projectile p, Rectangle own, Rectangle target);
        private delegate bool ProjectileSightOriginal(Projectile p, Entity target);
        private delegate bool ProjectileSightDetour(ProjectileSightOriginal original, Projectile p, Entity target);
        private delegate bool PlayerSightOriginal(Player player, Entity target);
        private delegate bool PlayerSightDetour(PlayerSightOriginal original, Player player, Entity target);
        private delegate void ProjectileOriginal(Projectile p);
        private delegate void ProjectileDetour(ProjectileOriginal original, Projectile p);
        private delegate void BombOriginal(Projectile p, Rectangle rectangle, int player);
        private delegate void BombDetour(BombOriginal original, Projectile p, Rectangle rectangle, int player);
        private delegate void MeleeOriginal(Player player, Item item, Rectangle hitbox, int damage, float knockback);
        private delegate void MeleeDetour(MeleeOriginal original, Player player, Item item, Rectangle hitbox, int damage, float knockback);
        private delegate bool? MeleeCollisionOriginal(Player player, Item item, Rectangle hitbox, NPC target);
        private delegate bool? MeleeCollisionDetour(MeleeCollisionOriginal original, Player player, Item item, Rectangle hitbox, NPC target);
        private delegate void PlayerOriginal(Player player);
        private delegate void PlayerDetour(PlayerOriginal original, Player player);
        private delegate void ReflectOriginal(NPC npc, Projectile p);
        private delegate void ReflectDetour(ReflectOriginal original, NPC npc, Projectile p);
        private delegate void JavelinOriginal(int index, int type, int target, Point[] buffer);
        private delegate void JavelinDetour(JavelinOriginal original, int index, int type, int target, Point[] buffer);

        private static MethodInfo Require(Type type, string name, params Type[] args)
            => type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, args, null) ?? throw new MissingMethodException(type.FullName, name);

        private void AddHook(MethodInfo method, Delegate detour)
        {
            var hook = new Hook(method, detour, false);
            hooks.Add(hook);
            hook.Apply();
        }

        public override void Load()
        {
            try
            {
                processMelee = Require(typeof(Player), "ProcessHitAgainstNPC", typeof(Item), typeof(Rectangle),
                    typeof(int), typeof(float), typeof(int)).CreateDelegate<Action<Player, Item, Rectangle, int, float, int>>();
                // 先登记再Apply，匹配失败时包括当前入口在内全部释放。
                var damage = new ILHook(Require(typeof(Projectile), nameof(Projectile.Damage)), PatchDamage, false);
                hooks.Add(damage); damage.Apply();
                AddHook(Require(typeof(Projectile), nameof(Projectile.Colliding), typeof(Rectangle), typeof(Rectangle)),
                    (CollisionDetour)((original, p, own, target) => BypassCollision(p, target) || original(p, own, target)));
                AddHook(Require(typeof(Projectile), nameof(Projectile.CanHitWithMeleeWeapon), typeof(Entity)),
                    (ProjectileSightDetour)((original, p, target) => IsVirtual(p) && ReferenceEquals(current.Target, target)
                        || original(p, target)));
                AddHook(Require(typeof(Player), nameof(Player.CanHit), typeof(Entity)),
                    (PlayerSightDetour)((original, player, target) => current?.Player == player && current.Projectile == null
                        && ReferenceEquals(current.Target, target) || original(player, target)));
                AddHook(Require(typeof(Projectile), nameof(Projectile.Kill)),
                    (ProjectileDetour)((original, p) => { if (!IsVirtual(p)) original(p); }));
                AddHook(Require(typeof(Projectile), "CutTiles"),
                    (ProjectileDetour)((original, p) => { if (!IsVirtual(p)) original(p); }));
                AddHook(Require(typeof(Projectile), "BombsHurtPlayers", typeof(Rectangle), typeof(int)),
                    (BombDetour)((original, p, box, player) => { if (!IsVirtual(p)) original(p, box, player); }));
                // 反射拒绝伤害的原分支仍保留，但不让虚拟接触反射正在远处飞行的原弹幕。
                AddHook(Require(typeof(NPC), nameof(NPC.ReflectProjectile), typeof(Projectile)),
                    (ReflectDetour)((original, npc, p) => { if (!IsVirtual(p)) original(npc, p); }));
                AddHook(Require(typeof(Projectile), nameof(Projectile.KillOldestJavelin), typeof(int), typeof(int), typeof(int), typeof(Point[])),
                    (JavelinDetour)((original, index, type, target, buffer) =>
                    { if (current?.Projectile?.whoAmI != index) original(index, type, target, buffer); }));
                AddHook(Require(typeof(Player), "ItemCheck_MeleeHitNPCs", typeof(Item), typeof(Rectangle), typeof(int), typeof(float)),
                    (MeleeDetour)ResolveMelee);
                AddHook(Require(typeof(Player), nameof(Player.ApplyAttackCooldown)),
                    (PlayerDetour)((original, player) =>
                    { if (current?.Player != player || current.Projectile != null) original(player); }));
                AddHook(Require(typeof(CombinedHooks), nameof(CombinedHooks.CanPlayerMeleeAttackCollideWithNPC),
                    typeof(Player), typeof(Item), typeof(Rectangle), typeof(NPC)),
                    (MeleeCollisionDetour)((original, player, item, box, target) =>
                        IsVirtualMelee(player, item, target) ? true : original(player, item, box, target)));
                Ready = true;
            }
            catch (Exception exception)
            {
                DisposeHooks();
                Mod.Logger.Warn("索命长矛已停用：安装版伤害入口不匹配。原武器攻击不受影响。", exception);
            }
        }

        private void DisposeHooks()
        {
            Ready = false;
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            processMelee = null;
        }

        public override void Unload()
        {
            DisposeHooks();
            ClearTransient();
        }

        public override void OnWorldUnload()
        { current = null; meleeSource = null; }

        private static void ClearTransient()
        { current = null; meleeSource = null; FailedProjectileTypes.Clear(); meleeFailed = false; }

        private static void PatchDamage(ILContext il)
        {
            // 必须在原版CanDamage(含原版特殊启动条件)通过之后，不在OnSpawn或PreAI猜测可伤害状态。
            Instruction[] hitboxes = il.Body.Instructions.Where(i => i.MatchCall<Projectile>("Damage_GetHitbox")).ToArray();
            if (hitboxes.Length != 1 || !hitboxes[0].Previous.MatchLdarg(0)
                || !il.Body.Instructions.TakeWhile(i => i != hitboxes[0])
                    .Any(i => i.MatchCall(typeof(ProjectileLoader), nameof(ProjectileLoader.CanDamage))))
                throw new InvalidOperationException("Lifetaking Damage eligibility checkpoint changed");
            var cursor = new ILCursor(il);
            cursor.Goto(hitboxes[0], MoveType.Before);
            cursor.Emit(OpCodes.Dup);
            cursor.EmitDelegate<Action<Projectile>>(ResolveProjectile);
        }

        internal static bool IsVirtual(Projectile p) => ReferenceEquals(current?.Projectile, p);
        internal static bool IsVirtualMelee(Player player, Item item, NPC target)
            => current != null && current.Projectile == null && current.Player == player
                && current.Item == item && ReferenceEquals(current.Target, target)
                && target.GetGlobalNPC<HookTargetIdentity>().Generation == current.Generation;
        internal static bool HasMeleeSource(Player player) => meleeSource == player;

        private static bool BypassCollision(Projectile p, Rectangle rectangle)
        {
            if (!IsVirtual(p) || !current.Entered || current.GeometryConsumed) return false;
            Rectangle expected = current.Target.Hitbox;
            if (current.Target.type == NPCID.SolarCrawltipedeTail) expected.Inflate(8, 8);
            if (expected != rectangle) return false;
            current.GeometryConsumed = true;
            return true;
        }

        internal static bool EnterVirtualMelee(Player player, Item item, NPC target)
        {
            if (!IsVirtualMelee(player, item, target) || current.Entered) return false;
            current.Entered = true;
            return true;
        }

        internal static bool EnterVirtualTarget(Projectile p, NPC target)
        {
            if (!IsVirtual(p) || current.Entered || !ReferenceEquals(current.Target, target)
                || target.GetGlobalNPC<HookTargetIdentity>().Generation != current.Generation) return false;
            current.Entered = true; // 在回调前锁住，OnHit内再次调用同一弹幕Damage也不能递归。
            return true;
        }

        private static void ResolveProjectile(Projectile p)
        {
            if (!Ready || IsVirtual(p) || Main.netMode == NetmodeID.Server || p.owner != Main.myPlayer
                || p.owner < 0 || p.owner >= Main.maxPlayers || !p.friendly || p.hostile || p.damage <= 0
                || FailedProjectileTypes.Contains(p.type)) return;
            Player owner = Main.player[p.owner];
            var data = p.GetGlobalProjectile<LifetakingGlobalProjectile>();
            if (!owner.active || owner.dead || !data.IsWeaponAttack(p)
                || !owner.TryGetModPlayer(out LifetakingPlayer state) || !state.TryGetTarget(out NPC target)) return;
            uint generation = target.GetGlobalNPC<HookTargetIdentity>().Generation;
            if (!data.Ledger.Reserve(target.whoAmI, generation, data.Phase(p), Main.GameUpdateCount, data.Interval(p))) return;
            var previous = current;
            var snapshot = new LifetakingProjectileSnapshot(p);
            current = new VirtualHit { Projectile = p, Player = owner, Target = target, Generation = generation };
            try
            {
                // 同一原实体、同一伤害入口；只允许印记目标碰撞，不生成代理伤害弹幕。
                p.Damage();
            }
            catch (Exception exception)
            {
                FailedProjectileTypes.Add(p.type);
                ModContent.GetInstance<LifetakingAttackSystem>().Mod.Logger.Warn(
                    $"索命对弹幕{p.type}的结算已停用，本轮加载不再重试。", exception);
            }
            finally
            {
                try { snapshot.Restore(p); }
                finally { current = previous; }
            }
        }

        private static void ResolveMelee(MeleeOriginal original, Player player, Item item, Rectangle box, int damage, float knockback)
        {
            Player previousSource = meleeSource;
            meleeSource = player;
            try
            {
                if (Ready && !meleeFailed && Main.netMode != NetmodeID.Server && player.whoAmI == Main.myPlayer
                    && !player.dead && !item.noMelee && damage > 0 && player.itemAnimation > 0
                    && player.GetModPlayer<LifetakingPlayer>() is { } state && state.TryGetTarget(out NPC target))
                {
                    uint generation = target.GetGlobalNPC<HookTargetIdentity>().Generation;
                    if (state.MeleeLedger.Reserve(target.whoAmI, generation, 0, Main.GameUpdateCount, 0)
                        && target.immune[player.whoAmI] == 0 && player.CanHitNPCWithMeleeHit(target.whoAmI) && player.attackCD <= 0)
                    {
                        VirtualHit previous = current;
                        current = new VirtualHit { Player = player, Item = item, Target = target, Generation = generation };
                        try { processMelee(player, item, box, damage, knockback, target.whoAmI); }
                        catch (Exception exception)
                        {
                            meleeFailed = true;
                            ModContent.GetInstance<LifetakingAttackSystem>().Mod.Logger.Warn("索命近战结算异常，已停用该入口。", exception);
                        }
                        finally { current = previous; }
                    }
                }
                original(player, item, box, damage, knockback);
            }
            finally { meleeSource = previousSource; }
        }
    }
}
