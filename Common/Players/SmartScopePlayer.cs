using System;
using System.IO;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Systems;
using TestMod.Content.Buffs;

namespace TestMod.Common.Players
{
    public class SmartScopePlayer : ModPlayer
    {
        internal const byte RequestPacket = 20, StatePacket = 21;
        internal const int ActiveDuration = 600, CooldownDuration = 600;
        public bool Equipped;
        internal int Remaining { get; private set; }
        private bool requested;
        private bool serverReady;
        private ulong lastTick = ulong.MaxValue;
        private int lastSent = -1;

        public override void ResetEffects() => Equipped = false;

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Player.whoAmI == Main.myPlayer && !Player.dead && !Main.blockInput &&
                SmartScopeKeybind.ActivateKey?.JustPressed == true)
                requested = true;
        }

        public override void PostUpdateEquips()
        {
            if (!requested) return;
            requested = false;
            // 请求晚于本帧装备注入消费；不使用上一帧的饰品标志。
            if (!Equipped || Player.dead || Player.CCed || Remaining != 0) return;
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                if (!serverReady || Player.whoAmI != Main.myPlayer) return;
                ModPacket packet = Mod.GetPacket();
                packet.Write(RequestPacket);
                packet.Write((byte)1);
                packet.Send();
            }
            else
            {
                TryActivate();
                SendState();
            }
        }

        internal bool TryActivate()
        {
            if (!Player.active || Player.dead || Player.CCed || !Equipped || Remaining != 0) return false;
            Player.AddBuff(ModContent.BuffType<SmartScopeActiveBuff>(), ActiveDuration);
            if (!Player.HasBuff(ModContent.BuffType<SmartScopeActiveBuff>())) return false;
            ClassicGrapeBeerSystem.RemoveOriginalBuff(Player);
            Remaining = ActiveDuration + CooldownDuration;
            return true;
        }

        public override void PreUpdate() => Tick(Player.dead);

        public override void UpdateDead()
        {
            Equipped = requested = false;
            Tick(true);
        }

        private void Tick(bool dead)
        {
            if (lastTick == Main.GameUpdateCount) return;
            lastTick = Main.GameUpdateCount;
            bool interrupted = dead && Remaining > CooldownDuration;
            if (interrupted)
            {
                Remaining = CooldownDuration;
                Player.ClearBuff(ModContent.BuffType<SmartScopeActiveBuff>());
            }
            if (Remaining > 0) Remaining--;
            if (!dead) RefreshCooldownDisplay();
            if ((interrupted || Remaining == CooldownDuration || Remaining == 0) && lastSent != Remaining)
                SendState();
        }

        private void RefreshCooldownDisplay()
        {
            if (Remaining > 0 && Remaining <= CooldownDuration)
                Player.AddBuff(ModContent.BuffType<SmartScopeCooldownBuff>(), Remaining + 1);
            else Player.ClearBuff(ModContent.BuffType<SmartScopeCooldownBuff>());
        }

        public override void SaveData(TagCompound tag)
        {
            // 退出时不保存主动效果，保留完整后置冷却；卸下和重戴不清计时。
            if (Remaining > 0) tag["smartScopeCooldown"] = Math.Min(Remaining, CooldownDuration);
        }

        public override void LoadData(TagCompound tag)
            => Remaining = Math.Clamp(tag.GetInt("smartScopeCooldown"), 0, CooldownDuration);

        public override void OnEnterWorld()
        {
            requested = Equipped = false;
            lastTick = ulong.MaxValue;
            lastSent = -1;
            Remaining = Math.Min(Remaining, CooldownDuration);
            serverReady = Main.netMode != NetmodeID.MultiplayerClient;
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = Mod.GetPacket();
                packet.Write(RequestPacket);
                packet.Write((byte)0);
                packet.Write((ushort)Remaining);
                packet.Send();
            }
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => SendState(toWho);

        private void SendState(int toWho = -1)
        {
            if (Main.netMode != NetmodeID.Server) return;
            ModPacket packet = Mod.GetPacket();
            packet.Write(StatePacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write((ushort)Remaining);
            packet.Write(serverReady);
            packet.Send(toWho);
            lastSent = Remaining;
        }

        internal static void ReceiveRequest(BinaryReader reader, int sender)
        {
            byte action = reader.ReadByte();
            int saved = action == 0 ? reader.ReadUInt16() : 0;
            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers || action > 1) return;
            Player player = Main.player[sender];
            if (!player.active || !player.TryGetModPlayer(out SmartScopePlayer state)) return;
            if (action == 0)
            {
                if (!state.serverReady)
                {
                    state.Remaining = Math.Max(state.Remaining, Math.Clamp(saved, 0, CooldownDuration));
                    state.serverReady = true;
                }
                state.SendState(sender);
            }
            else if (state.serverReady)
            {
                // 服务器在 PostUpdateEquips 中核对自己的装备和计时，不接收客户端自报持续时间。
                if (state.Remaining == 0 && !player.dead) state.requested = true;
                else state.SendState(sender);
            }
        }

        internal static void ReceiveState(BinaryReader reader)
        {
            int index = reader.ReadByte(), remaining = reader.ReadUInt16();
            bool ready = reader.ReadBoolean();
            if (Main.netMode != NetmodeID.MultiplayerClient || index >= Main.maxPlayers ||
                remaining > ActiveDuration + CooldownDuration ||
                !Main.player[index].TryGetModPlayer(out SmartScopePlayer state)) return;
            state.Remaining = remaining;
            state.serverReady = ready;
            if (remaining > CooldownDuration)
            {
                state.Player.AddBuff(ModContent.BuffType<SmartScopeActiveBuff>(), remaining - CooldownDuration);
                ClassicGrapeBeerSystem.RemoveOriginalBuff(state.Player);
            }
            else state.Player.ClearBuff(ModContent.BuffType<SmartScopeActiveBuff>());
            state.RefreshCooldownDisplay();
        }
    }
}
