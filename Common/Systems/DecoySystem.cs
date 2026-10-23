using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ID;
using TestMod.Content.NPCs;
using TestMod.Common.DataStructures;
using TestMod.Common.GlobalNPCs;
using DecoyProjectile = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Common.Systems
{
    internal readonly record struct EnemySource(int Slot, int Type, uint Generation)
    {
        internal bool Valid => Generation != 0 && Slot >= 0 && Slot < Main.maxNPCs && Type > 0;
        internal NPC Resolve()
        {
            if (!Valid) return null;
            NPC npc = Main.npc[Slot];
            return npc != null && DecoySystem.Eligible(npc) && (npc.type == Type ||
                npc.ModNPC != null && npc.ModNPC.AIType == npc.type && npc.GetGlobalNPC<DecoyState>().RegisteredType == Type) &&
                npc.GetGlobalNPC<HookTargetIdentity>().Generation == Generation ? npc : null;
        }
    }

    public sealed partial class DecoySystem : ModSystem
    {
        private readonly List<Hook> hooks = new();
        internal static bool Ready { get; private set; }
        internal static EnemySource CurrentSource => current?.Source ?? default;
        internal static DecoyHandle CurrentDecoy => current?.Decoy ?? default;
        internal static bool Eligible(NPC npc, bool requireAlive = true)
        {
            if (npc == null || requireAlive && (!npc.active || npc.life <= 0) || npc.friendly || npc.townNPC ||
                npc.ModNPC is ScarecrowDecoy) return false;
            int type = npc.type;
            if (npc.ModNPC != null && npc.ModNPC.AIType == type && npc.TryGetGlobalNPC(out DecoyState state) && state.RegisteredType > 0)
                type = state.RegisteredType;
            return type >= 0 && type < NPCID.Sets.CountsAsCritter.Length && !NPCID.Sets.CountsAsCritter[type];
        }
        private const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private delegate void NPCOriginal(NPC npc);
        private delegate void NPCDetour(NPCOriginal original, NPC npc);
        private delegate void ProjectileOriginal(Projectile projectile);
        private delegate void ProjectileDetour(ProjectileOriginal original, Projectile projectile);
        private delegate void NPCSpawnOriginal(NPC npc, IEntitySource source);
        private delegate void NPCSpawnDetour(NPCSpawnOriginal original, NPC npc, IEntitySource source);
        private delegate void ProjectileSpawnOriginal(Projectile projectile, IEntitySource source);
        private delegate void ProjectileSpawnDetour(ProjectileSpawnOriginal original, Projectile projectile, IEntitySource source);

        public override void Load()
        {
            reporter = this;
            Ready = false;
            try
            {
                Install(typeof(NPCLoader), nameof(NPCLoader.NPCAI), new[] { typeof(NPC) }, (NPCDetour)RunNPC);
                Install(typeof(ProjectileLoader), nameof(ProjectileLoader.ProjectileAI), new[] { typeof(Projectile) }, (ProjectileDetour)RunProjectile);
                Install(typeof(NPCLoader), "OnSpawn", new[] { typeof(NPC), typeof(IEntitySource) }, (NPCSpawnDetour)SpawnNPC);
                Install(typeof(ProjectileLoader), "OnSpawn", new[] { typeof(Projectile), typeof(IEntitySource) }, (ProjectileSpawnDetour)SpawnProjectile);
                Install(typeof(Projectile), nameof(Projectile.Kill), Type.EmptyTypes, (ProjectileDetour)KillProjectile);
                Install(typeof(NPCLoader), nameof(NPCLoader.OnKill), new[] { typeof(NPC) }, (NPCDetour)KillNPC);
                InstallDamageHooks();
                InstallTargetHooks();
                Ready = true;
            }
            catch (Exception exception)
            {
                DisposeHooks();
                for (int i = 0; i < rows.Count; i++) rows[i] = rows[i].Replace("\t已安装\t", "\t已撤销\t");
                rows.Add("核心入口\t停用\t" + exception.GetType().Name + ": " + exception.Message);
                Mod.Logger.Warn("[Decoy] 视图或真实伤害边界安装失败，整项目标欺骗停用。", exception);
            }
        }

        private void Install(Type type, string name, Type[] arguments, Delegate detour)
        {
            MethodInfo method = type.GetMethod(name, Methods, null, arguments, null)
                ?? throw new MissingMethodException(type.FullName, name);
            Hook hook = new(method, detour, false);
            try { hook.Apply(); hooks.Add(hook); }
            catch { hook.Dispose(); throw; }
            rows.Add(type.FullName + "::" + method + "\t已安装\t完整调用作用域");
        }

        private static void RunNPC(NPCOriginal original, NPC npc)
        {
            Frame frame = Enter(npc, true);
            try { original(npc); }
            finally { Leave(frame); }
        }

        private static void RunProjectile(ProjectileOriginal original, Projectile projectile)
        {
            Frame frame = Enter(projectile, true);
            try { original(projectile); }
            finally { Leave(frame); }
        }

        private static void SpawnNPC(NPCSpawnOriginal original, NPC npc, IEntitySource source)
        {
            npc.GetGlobalNPC<DecoyState>().BindParent(npc, source);
            Frame frame = Enter(npc, true);
            try { original(npc, source); }
            finally { Leave(frame); }
        }

        private static void SpawnProjectile(ProjectileSpawnOriginal original, Projectile projectile, IEntitySource source)
        {
            // 原版召唤 AI 也可能请求 ProjectileID.None；该占位实体没有 Global 实例。
            // 仍分发原 OnSpawn，但在真实视图中执行，不能认领外层敌人的假目标。
            if (!projectile.TryGetGlobalProjectile(out DecoyProjectile data))
            {
                Frame realFrame = Enter(null, false);
                try { original(projectile, source); }
                finally { Leave(realFrame); }
                return;
            }
            data.Decoy_OnSpawn(projectile, source);
            if (projectile.hostile && !data.DecoySource.Valid && source is not EntitySource_Sync and not TimeEchoAttackSource)
                Note(projectile, "无可校验敌人来源；按弹幕自身位置选择稻草人，不推断来源", false);
            Frame frame = Enter(projectile, true);
            try { original(projectile, source); }
            finally { Leave(frame); }
        }

        private static void KillProjectile(ProjectileOriginal original, Projectile projectile)
        {
            Frame frame = Enter(projectile, true);
            try { original(projectile); }
            finally { Leave(frame); }
        }

        private static void KillNPC(NPCOriginal original, NPC npc)
        {
            Frame frame = Enter(npc, true);
            try { original(npc); }
            finally { Leave(frame); }
        }

        internal static EnemySource SourceForSpawn(IEntitySource source)
        {
            if (source is TimeEchoAttackSource) return default;
            // 明确的无关来源也具有否决权，不能从外层敌人作用域认领玩家攻击。
            if (source is EntitySource_Parent parent) return SourceOf(parent.Entity);
            return CurrentSource;
        }

        private static NPC TargetFor(Entity entity, EnemySource source)
        {
            // 死亡回调仍可产生攻击；保留该回调的目标视图，身份存活校验另由来源解析完成。
            if (entity is NPC npc && !Eligible(npc, false)) return null;
            if (entity is Projectile shot && (!shot.hostile || shot.friendly ||
                !shot.TryGetGlobalProjectile(out DecoyProjectile shotData) ||
                shotData.IsTimeEchoAttack || shotData.DecoyExcluded)) return null;
            if (entity is not NPC && entity is not Projectile) return null;
            NPC origin = source.Resolve();
            NPC target = origin?.GetGlobalNPC<DecoyState>().GetTarget(origin);
            if (entity is NPC own) return target ?? own.GetGlobalNPC<DecoyState>().GetTarget(own);
            var projectile = (Projectile)entity;
            var data = projectile.GetGlobalProjectile<DecoyProjectile>();
            if (target != null)
            {
                DecoyHandle handle = DecoyHandle.Of(target);
                if (data.DecoySelection.Target != handle)
                {
                    data.DecoySelection.Inherit(handle);
                    if (Main.netMode != NetmodeID.MultiplayerClient) projectile.netUpdate = true;
                }
                return target;
            }
            target = data.DecoySelection.Get(projectile, out bool changed);
            if (changed) projectile.netUpdate = true;
            return target;
        }

        internal static bool InRealView(Player player, Func<bool> action)
        {
            Frame frame = Enter(player, false, true);
            try { return action(); }
            finally { Leave(frame); }
        }

        internal static EnemySource SourceOf(Entity entity)
        {
            if (entity is Projectile excluded &&
                (!excluded.TryGetGlobalProjectile(out DecoyProjectile excludedData) ||
                 excludedData.IsTimeEchoAttack || excludedData.DecoyExcluded)) return default;
            // OnKill 中其他 Global 可以先清实体状态；当前实体的已验证来源仍须传给死亡子攻击。
            // NPC Generation / projectile identity 改变时不复用旧调用作用域。
            for (Frame frame = current; frame != null; frame = frame.Parent)
                if (ReferenceEquals(frame.Entity, entity) && frame.Source.Valid &&
                    (entity is NPC scopedNPC && scopedNPC.GetGlobalNPC<HookTargetIdentity>().Generation == frame.NPCGeneration ||
                     entity is Projectile scopedProjectile && scopedProjectile.identity == frame.ProjectileIdentity &&
                     scopedProjectile.owner == frame.ProjectileOwner &&
                     ReferenceEquals(scopedProjectile.GetGlobalProjectile<DecoyProjectile>(), frame.ProjectileData))) return frame.Source;
            if (entity is Projectile projectile)
            {
                var data = projectile.GetGlobalProjectile<DecoyProjectile>();
                return data.IsTimeEchoAttack ? default : data.DecoySource;
            }
            if (entity is not NPC npc || !Eligible(npc, false)) return default;
            NPC actor = npc;
            DecoyState state = npc.GetGlobalNPC<DecoyState>();
            // 普通召唤敌人也是独立的诱敌主体；它发射的攻击跟随它自己的选择。
            // 仅 realLife / 明确部件映射归并到根，避免远处召唤物越过 2400 像素边界。
            if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife] is NPC root && Eligible(root))
                npc = root;
            else if (TryGetVanillaParent(npc, state.RegisteredType, out NPC parent)) npc = parent;
            if (state.ParentSource.Valid && !state.ParentFromSpawn)
                return state.ParentSource.Slot == npc.whoAmI && state.ParentSource.Resolve() != null ? state.ParentSource : default;
            int registeredType = npc.GetGlobalNPC<DecoyState>().RegisteredType;
            EnemySource source = new(npc.whoAmI, registeredType > 0 ? registeredType : npc.type,
                npc.GetGlobalNPC<HookTargetIdentity>().Generation);
            if (!ReferenceEquals(actor, npc) && source.Valid) { state.ParentSource = source; state.ParentFromSpawn = false; }
            return source;
        }

        private static bool TryGetVanillaParent(NPC npc, int registeredType, out NPC parent)
        {
            parent = null;
            int type;
            float slot;
            switch (registeredType > 0 ? registeredType : npc.type)
            {
                case Terraria.ID.NPCID.MoonLordHead:
                case Terraria.ID.NPCID.MoonLordHand:
                case Terraria.ID.NPCID.MoonLordFreeEye: type = Terraria.ID.NPCID.MoonLordCore; slot = npc.ai[3]; break;
                case Terraria.ID.NPCID.SkeletronHand: type = Terraria.ID.NPCID.SkeletronHead; slot = npc.ai[1]; break;
                case Terraria.ID.NPCID.PrimeCannon:
                case Terraria.ID.NPCID.PrimeSaw:
                case Terraria.ID.NPCID.PrimeVice:
                case Terraria.ID.NPCID.PrimeLaser: type = Terraria.ID.NPCID.SkeletronPrime; slot = npc.ai[1]; break;
                default: return false;
            }
            if (!float.IsFinite(slot) || slot < 0 || slot >= Main.maxNPCs || slot != (int)slot) return false;
            NPC candidate = Main.npc[(int)slot];
            if (candidate == null || !candidate.active || !candidate.boss || candidate.type != type) return false;
            parent = candidate;
            return true;
        }

        private void DisposeHooks()
        {
            Ready = false;
            ResetViews();
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear(); damageHitbox = null; directHitShapes.Clear();
        }

        public override void OnWorldUnload()
        {
            ResetViews();
            warnings.Clear();
            for (int i = 0; i < Main.maxNPCs; i++)
                if (Main.npc[i] is NPC npc && npc.TryGetGlobalNPC(out DecoyState state)) state.SetDefaults(npc);
        }

        public override void Unload()
        {
            DisposeHooks();
            rows.Clear(); warnings.Clear(); reporter = null;
        }
    }
}
