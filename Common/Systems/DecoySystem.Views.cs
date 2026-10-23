using System;
using System.Buffers;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using TestMod.Common.GlobalNPCs;
using DecoyProjectile = TestMod.Common.GlobalProjectiles.GlobalProjectile;

namespace TestMod.Common.Systems
{
    public sealed partial class DecoySystem
    {
        private sealed class Frame
        {
            internal Frame Parent;
            internal Entity Entity;
            internal EnemySource Source;
            internal DecoyHandle Decoy;
            internal bool Fake, CommitGeometry;
            internal uint Epoch;
            internal uint NPCGeneration;
            internal int ProjectileIdentity;
            internal int ProjectileOwner;
            internal DecoyProjectile ProjectileData;
            internal Vector2 Target;
            internal int Width, Height;
            internal void Clear()
            {
                Parent = null; Entity = null; Source = default; Decoy = default;
                Fake = CommitGeometry = false;
                Epoch = NPCGeneration = 0; ProjectileIdentity = -1;
                ProjectileOwner = -1; ProjectileData = null;
                Target = Vector2.Zero;
                Width = Height = 0;
            }
        }

        private struct PlayerGeometry
        {
            internal Player Player;
            internal Vector2 Position, OldPosition, Velocity, OldVelocity;
            private int width, height;
            private Vector2[] shadows, shadowSnapshot;
            internal PlayerGeometry(Player player)
            {
                this = default;
                Player = player;
                Capture();
            }
            internal void Capture()
            {
                Player player = Player;
                Position = player.position; OldPosition = player.oldPosition;
                Velocity = player.velocity; OldVelocity = player.oldVelocity;
                width = player.width; height = player.height;
                shadows = player.shadowPos;
                if (shadows == null || shadows.Length == 0) return;
                if (shadowSnapshot == null || shadowSnapshot.Length < shadows.Length)
                {
                    Release();
                    shadowSnapshot = ArrayPool<Vector2>.Shared.Rent(shadows.Length);
                }
                shadows.CopyTo(shadowSnapshot, 0);
            }
            internal void Release()
            {
                if (shadowSnapshot != null) ArrayPool<Vector2>.Shared.Return(shadowSnapshot);
                shadowSnapshot = null;
            }
            internal bool StillCurrent => Player.whoAmI >= 0 && Player.whoAmI < Main.maxPlayers &&
                ReferenceEquals(Main.player[Player.whoAmI], Player);
            internal void Restore()
            {
                if (!StillCurrent) return;
                Player.position = Position; Player.oldPosition = OldPosition;
                Player.velocity = Velocity; Player.oldVelocity = OldVelocity;
                Player.width = width; Player.height = height;
                Player.shadowPos = shadows;
                if (shadows?.Length > 0) Array.Copy(shadowSnapshot, shadows, shadows.Length);
            }
        }

        [ThreadStatic] private static Frame current, truthOwner;
        [ThreadStatic] private static Stack<Frame> pool;
        [ThreadStatic] private static List<PlayerGeometry> truth;
        [ThreadStatic] private static List<Player> roster;
        [ThreadStatic] private static ulong rosterTick;
        [ThreadStatic] private static bool hasRoster, viewApplied;
        private static uint epoch;

        private static Frame Enter(Entity entity, bool fake, bool commit = false)
        {
            // 嵌套调用先还原真实视图，来源/距离/稻草人目标的读取不能受到外层假目标污染。
            // 真实回调可能已经更新位置，然后再调用另一段 AI；先提交，避免子视图恢复旧快照。
            if (!viewApplied && current?.CommitGeometry == true) CommitTruth();
            RestoreTruth();
            Frame parent = current;
            Frame frame = null;
            try
            {
                EnemySource source = entity == null ? parent?.Source ?? default : SourceOf(entity);
                NPC decoy = fake && Ready ? TargetFor(entity, source) : null;
                pool ??= new();
                frame = pool.Count != 0 ? pool.Pop() : new Frame();
                frame.Parent = parent; frame.Entity = entity ?? parent?.Entity;
                frame.Source = source; frame.Epoch = epoch;
                frame.NPCGeneration = entity is NPC npc ? npc.GetGlobalNPC<HookTargetIdentity>().Generation : 0;
                frame.ProjectileIdentity = entity is Projectile projectile ? projectile.identity : -1;
                if (entity is Projectile shot)
                {
                    frame.ProjectileOwner = shot.owner;
                    shot.TryGetGlobalProjectile(out DecoyProjectile data);
                    frame.ProjectileData = data;
                }
                frame.CommitGeometry = commit;
                frame.Fake = decoy != null;
                frame.CommitGeometry |= !frame.Fake;
                frame.Decoy = decoy != null ? DecoyHandle.Of(decoy) : default;
                frame.Target = decoy?.Center ?? Vector2.Zero;
                frame.Width = decoy?.width ?? 0; frame.Height = decoy?.height ?? 0;
                current = frame;
                if (frame.Fake && truthOwner == null) CaptureTruth(frame);
                ApplyView(frame);
                return frame;
            }
            catch
            {
                RestoreTruth();
                if (truthOwner == frame) ReleaseTruth();
                current = parent;
                if (frame != null) { frame.Clear(); pool.Push(frame); }
                ApplyView(parent);
                throw;
            }
        }

        private static void CaptureTruth(Frame owner)
        {
            truth ??= new(Main.maxPlayers);
            roster ??= new(Main.maxPlayers);
            if (!hasRoster || rosterTick != Main.GameUpdateCount)
            {
                hasRoster = true; rosterTick = Main.GameUpdateCount; roster.Clear();
                for (int i = 0; i < Main.maxPlayers; i++)
                    if (Main.player[i] is { active: true, dead: false } player) roster.Add(player);
            }
            truth.Clear(); truthOwner = owner;
            foreach (Player player in roster)
                if (player.active && !player.dead && ReferenceEquals(Main.player[player.whoAmI], player)) truth.Add(new(player));
        }

        private static void RestoreTruth()
        {
            if (!viewApplied) return;
            foreach (PlayerGeometry value in truth) value.Restore();
            viewApplied = false;
        }

        private static void CommitTruth()
        {
            if (truthOwner == null) return;
            for (int i = 0; i < truth.Count; i++)
                if (truth[i].StillCurrent)
                {
                    PlayerGeometry value = truth[i]; value.Capture(); truth[i] = value;
                }
        }

        private static void ReleaseTruth()
        {
            if (truth != null)
                foreach (PlayerGeometry value in truth) value.Release();
            truth?.Clear(); truthOwner = null;
        }

        private static void ApplyView(Frame frame)
        {
            if (frame?.Fake != true || truthOwner == null || !Ready || frame.Decoy.Resolve() == null) return;
            // 先置位，应用中发生异常时也能完整回滚已写入的玩家。
            viewApplied = true;
            foreach (PlayerGeometry value in truth)
            {
                Player player = value.Player;
                if (!value.StillCurrent || !player.active || player.dead) continue;
                player.width = frame.Width; player.height = frame.Height;
                player.Center = frame.Target;
                player.oldPosition = player.position;
                player.velocity = player.oldVelocity = Vector2.Zero;
                if (player.shadowPos != null) Array.Fill(player.shadowPos, player.position);
            }
            if (frame.Entity is NPC npc) ForceTarget(npc, false);
        }

        private static void Leave(Frame frame)
        {
            // 切世界/卸载已经回滚并更换 epoch；不能再把旧世界的快照写回。
            if (frame.Epoch != epoch) { frame.Clear(); pool?.Push(frame); return; }
            try
            {
                RestoreTruth();
                if (frame.CommitGeometry) CommitTruth();
            }
            finally
            {
                Frame parent = frame.Parent;
                if (truthOwner == frame) ReleaseTruth();
                current = parent;
                frame.Clear(); pool.Push(frame);
                ApplyView(parent);
            }
        }

        private static void ResetViews()
        {
            RestoreTruth();
            epoch++;
            current = null;
            ReleaseTruth(); roster?.Clear(); pool?.Clear();
            hasRoster = viewApplied = false;
        }
    }
}
