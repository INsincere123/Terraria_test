using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.Tools;
using TestMod.Content.NPCs;

namespace TestMod.Common.Systems
{
    public sealed class ScarecrowSystem : ModSystem
    {
        internal const byte PlaceRequestPacket = 13;
        internal const int LifetimeTicks = 480, UseTicks = 30, RetargetTicks = 180;
        internal const float PlacementRange = 1000f, AttractionRange = 2400f;
        internal const float RangeSquared = AttractionRange * AttractionRange;
        private static readonly List<DecoyHandle> active = new();
        private static readonly DecoyHandle[] owned = new DecoyHandle[Main.maxPlayers];
        private static readonly ulong[] nextUse = new ulong[Main.maxPlayers];
        internal static uint Revision { get; private set; }

        internal static void Register(NPC npc)
        {
            DecoyHandle handle = DecoyHandle.Of(npc);
            if (!handle.Valid || active.Contains(handle)) return;
            active.Add(handle); Revision++;
            if (npc.ModNPC is ScarecrowDecoy decoy && decoy.Owner >= 0 && decoy.Owner < owned.Length)
                owned[decoy.Owner] = handle;
        }

        internal static void Remove(DecoyHandle handle)
        {
            if (!active.Remove(handle)) return;
            Revision++;
            for (int i = 0; i < owned.Length; i++)
                if (owned[i] == handle) owned[i] = default;
        }

        internal static DecoyHandle FindNearest(Vector2 center)
        {
            float bestDistance = RangeSquared;
            DecoyHandle best = default;
            foreach (DecoyHandle handle in active)
            {
                NPC npc = handle.Resolve();
                if (npc == null) continue;
                float distance = Vector2.DistanceSquared(center, npc.Center);
                if (distance < bestDistance || distance == bestDistance &&
                    (!best.Valid || handle.Slot < best.Slot || handle.Slot == best.Slot && handle.Generation < best.Generation))
                { best = handle; bestDistance = distance; }
            }
            return best;
        }

        internal static void RequestPlace(Player player, Vector2 center)
        {
            if (player.whoAmI != Main.myPlayer || !DecoySystem.Ready) return;
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = ModContent.GetInstance<ScarecrowSystem>().Mod.GetPacket();
                packet.Write(PlaceRequestPacket); packet.Write(center.X); packet.Write(center.Y); packet.Send();
            }
            else TryPlace(player.whoAmI, center);
        }

        internal static void ReceivePlaceRequest(BinaryReader reader, int sender)
        {
            Vector2 center = new(reader.ReadSingle(), reader.ReadSingle());
            if (Main.netMode == NetmodeID.Server) TryPlace(sender, center);
        }

        internal static bool CanPlace(Player player, Vector2 center)
        {
            if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || player is not { active: true, dead: false } ||
                Vector2.DistanceSquared(player.Center, center) > PlacementRange * PlacementRange) return false;
            Vector2 position = center - new Vector2(ScarecrowDecoy.BodyWidth, ScarecrowDecoy.BodyHeight) * .5f;
            if (position.X < 16 || position.Y < 16 || position.X + ScarecrowDecoy.BodyWidth > Main.maxTilesX * 16f - 16 ||
                position.Y + ScarecrowDecoy.BodyHeight > Main.maxTilesY * 16f - 16) return false;
            return !Collision.SolidCollision(position, ScarecrowDecoy.BodyWidth, ScarecrowDecoy.BodyHeight);
        }

        internal static bool TryPlace(int sender, Vector2 center)
        {
            if (!DecoySystem.Ready || Main.netMode == NetmodeID.MultiplayerClient || sender < 0 || sender >= Main.maxPlayers)
                return false;
            Player player = Main.player[sender];
            if (player == null) return false;
            return DecoySystem.InRealView(player, () => TryPlaceCore(player, center));
        }

        private static bool TryPlaceCore(Player player, Vector2 center)
        {
            int sender = player.whoAmI;
            if (player?.HeldItem?.ModItem is not ScarecrowLure || Main.GameUpdateCount < nextUse[sender] || !CanPlace(player, center))
                return false;
            // 放置/碰撞使用真实玩家视图；只有服务器能执行 NPC.NewNPC。
            int index = NPC.NewNPC(player.GetSource_ItemUse(player.HeldItem), (int)center.X,
                (int)(center.Y + ScarecrowDecoy.BodyHeight * .5f), ModContent.NPCType<ScarecrowDecoy>());
            if (index < 0 || index >= Main.maxNPCs || Main.npc[index].ModNPC is not ScarecrowDecoy decoy) return false;
            DecoyHandle previous = owned[sender];
            NPC npc = Main.npc[index]; npc.Center = center;
            decoy.Initialize(sender);
            nextUse[sender] = Main.GameUpdateCount + UseTicks;
            if (previous.Resolve()?.ModNPC is ScarecrowDecoy old) old.Despawn();
            npc.netUpdate = true;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: index);
            return true;
        }

        public override void PreUpdateNPCs()
        {
            // 只遍历已登记的稻草人，先移除失效项，再运行本 tick 敌人 AI。
            for (int i = active.Count - 1; i >= 0; i--)
            {
                DecoyHandle handle = active[i];
                NPC npc = Main.npc[handle.Slot];
                if (handle.Resolve() is NPC living)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient) ((ScarecrowDecoy)living.ModNPC).TickLifetime();
                    continue;
                }
                if (Main.netMode != NetmodeID.MultiplayerClient && npc?.ModNPC is ScarecrowDecoy decoy &&
                    DecoyHandle.Of(npc) == handle) decoy.Despawn();
                else Remove(handle);
            }
        }

        private static void Reset()
        {
            active.Clear(); Array.Clear(owned); Array.Clear(nextUse); Revision++;
        }
        public override void OnWorldUnload() => Reset();
        public override void Unload() => Reset();
    }
}
