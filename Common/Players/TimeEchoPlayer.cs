using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.DataStructures;
using TestMod.Common.Mechanics.Dashes;
using TestMod.Common.Systems;

namespace TestMod.Common.Players
{
    public sealed partial class TimeEchoPlayer : ModPlayer
    {
        private readonly TimeEchoTimeline<TimeEchoSnapshot> timeline = new();
        private ulong transitionTick = ulong.MaxValue;
        private uint notifiedGeneration;
        private bool? pendingSkill;
        private bool wasAlive;
        private bool useServerMirror;

        public bool HasAbility => useServerMirror ? remoteOwned : timeline.Owned;
        internal bool HasPhantom => !Player.dead && (Main.netMode == NetmodeID.MultiplayerClient ? remotePhantom : timeline.HasPhantom);
        internal int RewindCooldown => useServerMirror ? remoteRewind : timeline.RewindCooldown;
        internal int SwapCooldown => useServerMirror ? remoteSwap : timeline.SwapCooldown;
        internal TimeEchoSnapshot Phantom => Main.netMode == NetmodeID.MultiplayerClient ? remoteSnapshot : timeline.Phantom;

        public override void SaveData(TagCompound tag)
        {
            tag["timeEchoOwned"] = HasAbility;
            tag["timeEchoRewind"] = RewindCooldown;
            tag["timeEchoSwap"] = SwapCooldown;
            tag["timeEchoAttackWait"] = attackState.WaitTicks;
        }

        public override void LoadData(TagCompound tag)
        {
            useServerMirror = false;
            timeline.Restore(tag.GetBool("timeEchoOwned"), tag.GetInt("timeEchoRewind"), tag.GetInt("timeEchoSwap"));
            remoteOwned = timeline.Owned;
            remoteRewind = timeline.RewindCooldown;
            remoteSwap = timeline.SwapCooldown;
            attackState.Restore(timeline.Owned ? tag.GetInt("timeEchoAttackWait") : 0);
        }

        public override void OnEnterWorld()
        {
            // netMode 在退出期间可能先变化；存档始终读取最后一次游戏会话的权威来源。
            if (useServerMirror) timeline.Restore(remoteOwned, remoteRewind, remoteSwap);
            else
            {
                remoteOwned = timeline.Owned;
                remoteRewind = timeline.RewindCooldown;
                remoteSwap = timeline.SwapCooldown;
            }
            useServerMirror = Main.netMode == NetmodeID.MultiplayerClient;
            ResetConnection();
            wasAlive = !Player.dead;
            if (Main.netMode == NetmodeID.MultiplayerClient)
                SendJoin();
        }

        internal void ClearTransient()
        {
            ClearEchoAttacks();
            ClearBonuses();
            timeline.ClearHistory();
            pendingSkill = null;
            transitionTick = ulong.MaxValue;
            remotePhantom = false;
            remoteRecording = false;
            ClearVisuals();
        }

        public override void UpdateDead()
        {
            ClearEchoAttacks();
            ClearBonuses();
            // 冷却只由系统每 tick 统一递减，死亡钩子只清历史，避免双重计时。
            if (timeline.Recording) timeline.ClearHistory();
            remotePhantom = false;
            pendingSkill = null;
            ClearVisuals();
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead || !HasPhantom || Main.gameMenu ||
                Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.blockInput ||
                PlayerInput.WritingText || Main.mapFullscreen || Main.playerInventory || Player.mouseInterface)
                return;
            if (TimeEchoSystem.RewindKey?.JustPressed == true) { pendingSkill = true; pendingAttack = false; }
            else if (TimeEchoSystem.SwapKey?.JustPressed == true) { pendingSkill = false; pendingAttack = false; }
            else if (TimeEchoSystem.AttackKey?.JustPressed == true) pendingAttack = true;
        }

        internal void UpdateEcho()
        {
            UpdateGuideChat();
            TickBonuses();
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // 进入世界时玩家激活包可能尚未到服务端；有界重试导入，不重置已建立的服务端会话。
                if (Player.whoAmI == Main.myPlayer && !joinConfirmed && Main.GameUpdateCount % 60 == 0)
                    SendJoin();
                if (remoteRewind > 0) remoteRewind--;
                if (remoteSwap > 0) remoteSwap--;
                if (Player.whoAmI == Main.myPlayer && HasAbility)
                    SendObservedResources();
                if (pendingSkill.HasValue)
                {
                    bool rewind = pendingSkill.Value;
                    pendingSkill = null;
                    if (HasPhantom && (rewind ? RewindCooldown : SwapCooldown) == 0)
                        SendSkillRequest(rewind);
                }
                if (pendingAttack)
                {
                    pendingAttack = false;
                    if (HasAbility && HasPhantom && attackState.WaitTicks == 0 && TimeEchoAttackSystem.Ready)
                        NewRequest(Request.Attack).Send();
                }
                UpdateVisuals();
                return;
            }

            if (Main.netMode == NetmodeID.Server && !sessionInitialized) return;
            if (awaitingRelocation && ++relocationAge > 180) awaitingRelocation = false;
            bool oldPhantom = timeline.HasPhantom;
            uint oldGeneration = timeline.RecordingGeneration;
            bool alive = !Player.dead && Player.statLife > 0;
            bool lifeChanged = wasAlive != alive;
            wasAlive = alive;
            if (!alive)
            {
                hasObservedResources = false;
                awaitingRelocation = false;
            }

            // 先写本 tick，才能在消费请求时取到精确的 t-300，而不是上一 tick 的目标。
            if (transitionTick != Main.GameUpdateCount)
                timeline.Tick(alive, CaptureHistory());

            // 位移发生在玩家常规更新完成后，避免本帧碰撞把落点提前推走。
            if (pendingSkill.HasValue)
            {
                bool rewind = pendingSkill.Value;
                pendingSkill = null;
                TryActivate(rewind);
            }
            ConsumeAttackRequest();
            NotifyRecording(timeline.RecordingGeneration, timeline.Recording);
            if (Main.netMode == NetmodeID.Server && (lifeChanged || oldPhantom != timeline.HasPhantom ||
                oldGeneration != timeline.RecordingGeneration || (timeline.Owned && Main.GameUpdateCount % 6 == 0)))
                SendState();
            UpdateVisuals();
        }

        private void NotifyRecording(uint generation, bool recording)
        {
            if (!recording || generation == notifiedGeneration) return;
            notifiedGeneration = generation;
            if (!Main.dedServ && Player.whoAmI == Main.myPlayer && !Player.dead)
                Main.NewText(Language.GetTextValue("Mods.TestMod.TimeEcho.Recording"), new Color(155, 155, 255));
        }

        internal void ChangeAbility(bool enabled)
        {
            ClearEchoAttacks();
            attackState.Clear();
            ClearBonuses();
            hasObservedResources = false;
            awaitingRelocation = false;
            if (enabled) timeline.Acquire(CaptureHistory());
            else timeline.Restore(false, 0, 0);
            transitionTick = Main.GameUpdateCount;
            pendingSkill = null;
            ClearVisuals();
            NotifyRecording(timeline.RecordingGeneration, timeline.Recording);
            if (Main.netMode == NetmodeID.Server) SendState();
        }

        private bool TryActivate(bool rewind)
        {
            if (Player.dead || Player.statLife <= 0 || !timeline.TryGetTarget(rewind, out TimeEchoSnapshot target)) return false;
            if (!float.IsFinite(target.Position.X) || !float.IsFinite(target.Position.Y)) return false;
            Vector2 origin = Player.position;
            ApplyDisplacement(target.Position, rewind);
            if (rewind)
            {
                bool observed = Main.netMode == NetmodeID.Server && hasObservedResources;
                Player.statLife = RestoreResource(observed ? observedLife : Player.statLife, target.Life,
                    observed ? observedLifeMax : Player.statLifeMax2);
                Player.statMana = RestoreResource(observed ? observedMana : Player.statMana, target.Mana,
                    observed ? observedManaMax : Player.statManaMax2);
                observedLife = Player.statLife;
                observedMana = Player.statMana;
            }
            timeline.Consume(rewind);
            StartBonuses(rewind);
            transitionTick = Main.GameUpdateCount;
            BlockQueuedAttacks();
            ClearVisuals();
            NotifyRecording(timeline.RecordingGeneration, timeline.Recording);
            if (Main.netMode == NetmodeID.Server)
            {
                // 远距离移动后发送目标区域；不能以区块未加载为由拒绝技能。
                RemoteClient.CheckSection(Player.whoAmI, target.Position);
                SendSuccess(rewind, origin);
                SendState();
            }
            else TimeEchoSystem.PlayDisplacement(origin + Player.Size * 0.5f, Player.Center, rewind);
            return true;
        }

        internal static int RestoreResource(int current, int historical, int maximum)
            => TimeEchoRules.RestoreResource(current, historical, maximum);

        private void ApplyDisplacement(Vector2 destination, bool rewind)
        {
            Vector2 velocity = Player.velocity;
            Player.RemoveAllGrapplingHooks();
            Player.GetModPlayer<DashPlayer>().CancelForTimeEcho();
            Player.GetModPlayer<SuperRelocatorPlayer>().CancelPositionFreeze();
            Player.dash = 0;
            Player.dashTime = 0;
            Player.eocDash = 0;
            // 负值代表正在冲刺；转入正常冷却，不清除已有正值冷却。
            if (Player.dashDelay < 0) Player.dashDelay = 30;
            Player.position = destination;
            Player.oldPosition = destination;
            Player.velocity = velocity;
            Player.fallStart = Player.fallStart2 = (int)(destination.Y / 16f);
            Player.ResetAdvancedShadows();
            int immunity = rewind ? 60 : 15;
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, immunity);
            for (int i = 0; i < Player.hurtCooldowns.Length; i++)
                Player.hurtCooldowns[i] = Math.Max(Player.hurtCooldowns[i], immunity);
            if (!Main.dedServ && Player.whoAmI == Main.myPlayer)
            {
                // 不调用会改变湿润、Buff 等状态的原版 Teleport；只刷新远距离相机/地图。
                Main.SetCameraLerp(0.1f, 0);
                Main.maxQ = true;
                Main.renderNow = true;
            }
        }
    }
}
