using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Systems;
using TestMod.Content.Items.Tools;

namespace TestMod.Common.Players
{
    public sealed partial class TimeEchoPlayer
    {
        internal const byte RequestPacket = 4;
        internal const byte StatePacket = 5;
        internal const byte SuccessPacket = 6;
        private enum Request : byte { Join, Acquire, Remove, Rewind, Swap, Resources, Attack }
        private bool sessionInitialized;
        private bool joinConfirmed;
        private uint revision, receivedRevision, lastSuccessRevision;
        private bool remoteOwned, remotePhantom, remoteRecording;
        private int remoteRewind, remoteSwap;
        private TimeEchoSnapshot remoteSnapshot;
        private ulong lastResourceTick;
        private int observedLife, observedMana, observedLifeMax, observedManaMax;
        private bool hasObservedResources;
        private bool awaitingRelocation;
        private int relocationAge;
        private Vector2 relocationPosition;

        private ModPacket NewRequest(Request request)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write(RequestPacket);
            packet.Write((byte)request);
            return packet;
        }

        private void SendJoin()
        {
            // 初次角色导入沿原版客户端角色存档信任模型；服务器只接受本连接一次。
            ModPacket packet = NewRequest(Request.Join);
            packet.Write(timeline.Owned);
            packet.Write((ushort)timeline.RewindCooldown);
            packet.Write((ushort)timeline.SwapCooldown);
            packet.Write((ushort)attackState.WaitTicks);
            packet.Send();
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            if (Main.netMode == NetmodeID.Server && sessionInitialized) SendState(toWho);
            else if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer) SendJoin();
        }

        internal void RequestAbilityItem(bool acquire)
        {
            if (Main.netMode == NetmodeID.SinglePlayer) ChangeAbility(acquire);
            else if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
            {
                ModPacket packet = NewRequest(acquire ? Request.Acquire : Request.Remove);
                packet.Write((byte)Player.selectedItem);
                packet.Send();
            }
        }

        private void SendSkillRequest(bool rewind)
        {
            SendObservedResources(true);
            // 不携带目的地、历史资源或无敌时间。
            NewRequest(rewind ? Request.Rewind : Request.Swap).Send();
        }

        private void SendObservedResources(bool force = false)
        {
            if (!force && Main.GameUpdateCount - lastResourceTick < 6) return;
            lastResourceTick = Main.GameUpdateCount;
            ModPacket packet = NewRequest(Request.Resources);
            packet.Write(lastSuccessRevision);
            packet.Write(Player.statLife); packet.Write(Player.statMana);
            packet.Write(Player.statLifeMax2); packet.Write(Player.statManaMax2);
            packet.Send();
        }

        private TimeEchoSnapshot CaptureHistory()
        {
            TimeEchoSnapshot sample = TimeEchoSnapshot.Capture(Player);
            // 客户端收到成功事件前仍可能发来旧位置；这段往返只记录服务器刚确定的落点。
            if (Main.netMode == NetmodeID.Server && awaitingRelocation)
                sample = sample with { Position = relocationPosition };
            // 服务器历史保存它实际收到的资源，不假定服务器会模拟客户端的魔力/饰品更新。
            return Main.netMode == NetmodeID.Server && hasObservedResources
                ? sample with { Life = observedLife, Mana = observedMana } : sample;
        }

        internal static void ReceiveRequest(BinaryReader reader, int sender)
        {
            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers) return;
            Request request = (Request)reader.ReadByte();
            Player player = Main.player[sender];
            if (!player.active) return;
            if (!player.TryGetModPlayer(out TimeEchoPlayer state)) return;
            if (request == Request.Join)
            {
                bool owned = reader.ReadBoolean();
                int rewind = reader.ReadUInt16(), swap = reader.ReadUInt16(), attackWait = reader.ReadUInt16();
                if (state.sessionInitialized)
                {
                    state.SendState(sender);
                    return;
                }
                state.sessionInitialized = true;
                state.timeline.Restore(owned, rewind, swap);
                state.attackState.Restore(owned ? attackWait : 0);
                state.SendState();
                return;
            }
            if (!state.sessionInitialized) return;
            if (request == Request.Resources)
            {
                uint acknowledgement = reader.ReadUInt32();
                int life = reader.ReadInt32(), mana = reader.ReadInt32();
                int lifeMax = reader.ReadInt32(), manaMax = reader.ReadInt32();
                if (!state.timeline.Owned || player.dead || acknowledgement != state.lastSuccessRevision ||
                    life <= 0 || mana < 0 || lifeMax <= 0 || manaMax < 0 || life > lifeMax || mana > manaMax)
                    return;
                state.observedLife = life; state.observedMana = mana;
                state.observedLifeMax = lifeMax; state.observedManaMax = manaMax;
                state.hasObservedResources = true;
                state.awaitingRelocation = false;
                return;
            }
            if (player.dead || player.statLife <= 0) return;
            if (request == Request.Acquire || request == Request.Remove)
            {
                int slot = reader.ReadByte();
                bool acquire = request == Request.Acquire;
                int type = acquire ? ModContent.ItemType<TimeEchoCore>() : ModContent.ItemType<TimeEchoDisruptor>();
                if (slot != player.selectedItem || slot >= player.inventory.Length ||
                    player.inventory[slot].type != type || player.inventory[slot].stack <= 0 ||
                    state.timeline.Owned == acquire)
                    return;
                // 多人只由服务器消耗；客户端 ConsumeItem 禁止再次扣除。
                Item item = player.inventory[slot];
                if (--item.stack <= 0) item.TurnToAir();
                NetMessage.SendData(MessageID.SyncEquipment, number: sender, number2: slot);
                state.ChangeAbility(acquire);
            }
            else if (request == Request.Rewind || request == Request.Swap)
            {
                // 每 tick 最多保留一个请求，回溯优先；消费时再次验证所有状态。
                bool rewind = request == Request.Rewind;
                if (rewind || !state.pendingSkill.HasValue) state.pendingSkill = rewind;
                state.pendingAttack = false;
            }
            else if (request == Request.Attack && !state.pendingSkill.HasValue) state.pendingAttack = true;
        }

        private void SendState(int toWho = -1)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write(StatePacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write(++revision);
            packet.Write(timeline.Owned);
            packet.Write((ushort)timeline.RewindCooldown);
            packet.Write((ushort)timeline.SwapCooldown);
            packet.Write(timeline.RecordingGeneration);
            packet.Write((ushort)rewindBonusTicks);
            packet.Write((ushort)swapBonusTicks);
            packet.Write((ushort)attackState.ActiveTicks);
            packet.Write((ushort)attackState.WaitTicks);
            packet.Write(attackState.Generation);
            packet.Write(timeline.Recording && !Player.dead);
            packet.Write(timeline.HasPhantom && !Player.dead);
            if (timeline.HasPhantom && !Player.dead) timeline.Phantom.WriteVisual(packet);
            packet.Send(toWho);
        }

        internal static void ReceiveState(BinaryReader reader)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            int index = reader.ReadByte();
            uint serial = reader.ReadUInt32();
            if (index >= Main.maxPlayers) return;
            if (Main.player[index] == null || !Main.player[index].TryGetModPlayer(out TimeEchoPlayer state)) return;
            if (serial <= state.receivedRevision) return;
            // 完整解码后才提交，截断包交给入口捕获，不留下半个状态。
            bool owned = reader.ReadBoolean();
            int rewind = reader.ReadUInt16(), swap = reader.ReadUInt16();
            uint generation = reader.ReadUInt32();
            int rewindBonus = reader.ReadUInt16(), swapBonus = reader.ReadUInt16();
            int attackActive = reader.ReadUInt16(), attackWait = reader.ReadUInt16();
            uint attackGeneration = reader.ReadUInt32();
            bool recording = reader.ReadBoolean(), phantom = reader.ReadBoolean();
            TimeEchoSnapshot snapshot = phantom ? TimeEchoSnapshot.ReadVisual(reader) : default;
            state.receivedRevision = serial;
            state.useServerMirror = true;
            if (index == Main.myPlayer) state.joinConfirmed = true;
            bool previousPhantom = state.remotePhantom;
            state.remoteOwned = owned;
            state.attackState.Synchronize(attackActive, attackWait, attackGeneration);
            if (!owned || state.Player.dead) state.ClearEchoAttacks();
            state.SyncBonuses(rewindBonus, swapBonus);
            state.remoteRewind = rewind;
            state.remoteSwap = swap;
            state.remoteRecording = recording;
            state.remotePhantom = phantom;
            if (state.remotePhantom)
            {
                state.remoteSnapshot = snapshot;
                state.ReceiveVisualTarget(state.remoteSnapshot, previousPhantom);
            }
            else state.ClearVisuals();
            state.NotifyRecording(generation, state.remoteRecording);
        }

        private void SendSuccess(bool rewind, Vector2 origin)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write(SuccessPacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write(++revision);
            lastSuccessRevision = revision;
            awaitingRelocation = true;
            relocationAge = 0;
            relocationPosition = Player.position;
            packet.Write(rewind);
            packet.Write(origin.X); packet.Write(origin.Y);
            packet.Write(Player.position.X); packet.Write(Player.position.Y);
            packet.Write(Player.statLife); packet.Write(Player.statMana);
            packet.Write((ushort)timeline.RewindCooldown); packet.Write((ushort)timeline.SwapCooldown);
            packet.Send();
        }

        internal static void ReceiveSuccess(BinaryReader reader)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            int index = reader.ReadByte();
            uint serial = reader.ReadUInt32();
            if (index >= Main.maxPlayers) return;
            if (Main.player[index] == null || !Main.player[index].TryGetModPlayer(out TimeEchoPlayer state)) return;
            if (serial <= state.receivedRevision) return;
            bool rewind = reader.ReadBoolean();
            Vector2 origin = new(reader.ReadSingle(), reader.ReadSingle());
            Vector2 destination = new(reader.ReadSingle(), reader.ReadSingle());
            int life = reader.ReadInt32(), mana = reader.ReadInt32();
            int rewindCooldown = reader.ReadUInt16(), swapCooldown = reader.ReadUInt16();
            state.receivedRevision = state.lastSuccessRevision = serial;
            state.remoteRewind = rewindCooldown;
            state.remoteSwap = swapCooldown;
            state.remotePhantom = false;
            state.BlockQueuedAttacks();
            state.ClearVisuals();
            // 网络往返期间已经死亡时，只接收冷却，不复活或移动尸体。
            if (state.Player.dead || !state.Player.active) return;
            state.ApplyDisplacement(destination, rewind);
            state.StartBonuses(rewind);
            // 换位不写资源；回溯只提高，避免网络往返期间的喝药/回复被旧结果覆盖。
            if (rewind)
            {
                state.Player.statLife = index == Main.myPlayer
                    ? RestoreResource(state.Player.statLife, life, state.Player.statLifeMax2) : life;
                state.Player.statMana = index == Main.myPlayer
                    ? RestoreResource(state.Player.statMana, mana, state.Player.statManaMax2) : mana;
            }
            TimeEchoSystem.PlayDisplacement(origin + state.Player.Size * 0.5f, state.Player.Center, rewind);
            if (index == Main.myPlayer)
            {
                NetMessage.SendData(MessageID.PlayerControls, number: index);
                NetMessage.SendData(MessageID.PlayerLifeMana, number: index);
                NetMessage.SendData(MessageID.PlayerMana, number: index);
                state.SendObservedResources(true);
            }
        }

        internal void ResetConnection()
        {
            ResetGuideChat();
            // 同一角色对象可能直接换世界；将最新服务端镜像转存，不能退回最初 LoadData 的冷却。
            if (useServerMirror)
                timeline.Restore(remoteOwned, remoteRewind, remoteSwap);
            ClearTransient();
            clearedAttackGeneration = 0;
            attackStartedTick = attackBlockedTick = ulong.MaxValue;
            ReleaseVisuals();
            sessionInitialized = false;
            joinConfirmed = false;
            revision = receivedRevision = lastSuccessRevision = notifiedGeneration = 0;
            hasObservedResources = false;
            awaitingRelocation = false;
            relocationAge = 0;
            lastResourceTick = 0;
            wasAlive = false;
            if (Main.netMode == NetmodeID.Server) timeline.Restore(false, 0, 0);
            if (Main.netMode == NetmodeID.Server) attackState.Clear();
        }
    }
}
