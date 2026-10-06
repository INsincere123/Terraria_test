using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.GlobalNPCs;
using TestMod.Content.Items.BossSummons;

namespace TestMod.Common.Systems
{
    public class CultistSummonSystem : ModSystem
    {
        internal const byte SummonRequestPacket = 1;
        internal const byte SummonSoundPacket = 2;
        private ILHook _deathEventsHook;
        internal static bool IsReady { get; private set; }

        public override void Load()
        {
            IsReady = false;
            try
            {
                MethodInfo method = typeof(NPC).GetMethod("DoDeathEvents",
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Player) }, null);
                if (method == null)
                    throw new MissingMethodException(typeof(NPC).FullName, "DoDeathEvents(Player)");
                _deathEventsHook = new ILHook(method, PatchDeathEvents);
                IsReady = true;
            }
            catch (Exception exception)
            {
                _deathEventsHook?.Dispose();
                _deathEventsHook = null;
                Mod.Logger.Warn($"[CultistSummon] 无法安装四柱控制钩子，召唤物停用：{exception}");
            }
        }

        public override void Unload()
        {
            RestoreRitualProtection();
            IsReady = false;
            _deathEventsHook?.Dispose();
            _deathEventsHook = null;
        }

        public override void OnWorldUnload() => RestoreRitualProtection();

        internal static bool HasItemSummonedCultist()
        {
            int index = NPC.FindFirstNPC(NPCID.CultistBoss);
            return index >= 0 && Main.npc[index].GetGlobalNPC<CultistSummonGlobalNPC>().IsItemSummoned;
        }

        internal static void RefreshRitualProtection()
        {
            // 成功召唤/收到 Boss 来源同步时立即保护，避免等教徒下一次 AI 才开始生效。
            foreach (NPC npc in Main.npc)
            {
                if (npc != null && npc.active &&
                    npc.TryGetGlobalNPC(out CultistRitualProtectionGlobalNPC protection))
                    protection.UpdateProtection(npc);
            }
        }

        private static void RestoreRitualProtection()
        {
            if (Main.npc == null)
                return;
            foreach (NPC npc in Main.npc)
            {
                if (npc != null && npc.TryGetGlobalNPC(out CultistRitualProtectionGlobalNPC protection))
                    protection.Restore(npc);
            }
        }

        private static void PatchDeathEvents(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before,
                instruction => instruction.MatchCall(typeof(WorldGen), nameof(WorldGen.TriggerLunarApocalypse))))
                throw new InvalidOperationException("找不到 TriggerLunarApocalypse 调用。");

            int callIndex = cursor.Index;
            cursor.Index++;
            if (cursor.TryGotoNext(instruction =>
                instruction.MatchCall(typeof(WorldGen), nameof(WorldGen.TriggerLunarApocalypse))))
                throw new InvalidOperationException("TriggerLunarApocalypse 调用不唯一。");

            cursor.Index = callIndex;
            // 只跨过原版启动月亮事件的调用；前面的击败标记、后面的死亡流程均保留。
            var afterTrigger = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<NPC, bool>>(ShouldSuppressPillars);
            cursor.Emit(OpCodes.Brtrue, afterTrigger);
            cursor.Index++;
            cursor.MarkLabel(afterTrigger);
        }

        private static bool ShouldSuppressPillars(NPC npc) => npc.type == NPCID.CultistBoss &&
            npc.GetGlobalNPC<CultistSummonGlobalNPC>().SuppressPillars;

        internal static bool CanSummon(Player player)
        {
            return IsReady && player.active && !player.dead && NPC.downedGolemBoss &&
                !NPC.AnyNPCs(NPCID.CultistBoss) && !NPC.LunarApocalypseIsUp &&
                !NPC.TowerActiveSolar && !NPC.TowerActiveVortex &&
                !NPC.TowerActiveNebula && !NPC.TowerActiveStardust &&
                !NPC.AnyNPCs(NPCID.LunarTowerSolar) && !NPC.AnyNPCs(NPCID.LunarTowerVortex) &&
                !NPC.AnyNPCs(NPCID.LunarTowerNebula) && !NPC.AnyNPCs(NPCID.LunarTowerStardust) &&
                NPC.MoonLordCountdown <= 0 && !NPC.AnyNPCs(NPCID.MoonLordCore);
        }

        internal static void RequestSummon(Player player, bool spawnPillars)
        {
            if (!CanSummon(player))
                return;
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // 不发送玩家编号或位置；服务器以发送者身份和服务器位置为准。
                ModPacket packet = ModContent.GetInstance<CultistSummonSystem>().Mod.GetPacket();
                packet.Write(SummonRequestPacket);
                packet.Write((byte)(spawnPillars ? 1 : 0));
                packet.Send();
            }
            else
                TrySummon(player, spawnPillars);
        }

        internal static void ReceiveSummonRequest(BinaryReader reader, int sender)
        {
            if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers)
                return;
            byte mode = reader.ReadByte();
            if (mode <= 1)
                TrySummon(Main.player[sender], mode == 1);
        }

        private static void TrySummon(Player player, bool spawnPillars)
        {
            // 再次检查使同帧多人请求只生成一只，也拒绝切换物品后到达的请求。
            if (Main.netMode == NetmodeID.MultiplayerClient || !CanSummon(player) ||
                player.HeldItem.ModItem is not CultistSummon item || item.SpawnPillars != spawnPillars)
                return;

            Vector2 position = player.Center + new Vector2(Main.rand.Next(-800, 801), -Main.rand.Next(250, 801));
            position.X = MathHelper.Clamp(position.X, 160f, Main.maxTilesX * 16f - 160f);
            position.Y = MathHelper.Clamp(position.Y, 160f, Main.maxTilesY * 16f - 160f);
            int index = NPC.NewNPC(NPC.GetBossSpawnSource(player.whoAmI), (int)position.X,
                (int)position.Y, NPCID.CultistBoss, Target: player.whoAmI);
            if (index >= Main.maxNPCs)
                return;

            NPC boss = Main.npc[index];
            CultistSummonGlobalNPC summonState = boss.GetGlobalNPC<CultistSummonGlobalNPC>();
            // 规则在生成时固定；物品之后切换模式不会改变已生成 Boss 的死亡行为。
            summonState.SuppressPillars = !spawnPillars;
            summonState.IsItemSummoned = true;
            RefreshRitualProtection();
            boss.netUpdate = true;
            string name = Lang.GetNPCNameValue(NPCID.CultistBoss);
            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(MessageID.SyncNPC, number: index);
                if (spawnPillars)
                    ClearRitualForSummon();
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Announcement.HasAwoken", name),
                    new Color(175, 75, 255));
                ModPacket packet = ModContent.GetInstance<CultistSummonSystem>().Mod.GetPacket();
                packet.Write(SummonSoundPacket);
                packet.Write(player.Center.X);
                packet.Write(player.Center.Y);
                packet.Send();
            }
            else
            {
                if (spawnPillars)
                    ClearRitualForSummon();
                Main.NewText(Language.GetTextValue("Announcement.HasAwoken", name), new Color(175, 75, 255));
                SoundEngine.PlaySound(SoundID.Roar, player.Center);
            }
        }

        private static void ClearRitualForSummon()
        {
            // 先移除碑，再杀教徒；整个过程发生在服务器同一次调用中，不给碑运行召唤 AI 的机会。
            bool removedTablet = false;
            foreach (NPC npc in Main.npc)
            {
                if (npc == null || !npc.active || npc.type != NPCID.CultistTablet)
                    continue;
                if (npc.TryGetGlobalNPC(out CultistRitualProtectionGlobalNPC protection))
                    protection.Restore(npc);
                npc.active = false;
                removedTablet = true;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
            }
            if (removedTablet)
                CultistRitual.TabletDestroyed();

            foreach (NPC npc in Main.npc)
            {
                if (npc == null || !npc.active ||
                    (npc.type != NPCID.CultistArcherBlue && npc.type != NPCID.CultistDevote))
                    continue;
                npc.GetGlobalNPC<CultistRitualProtectionGlobalNPC>().KillForSummon(npc);
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
            }
        }

        internal static void ReceiveSummonSound(BinaryReader reader)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;
            Vector2 position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            if (float.IsFinite(position.X) && float.IsFinite(position.Y))
                SoundEngine.PlaySound(SoundID.Roar, position);
        }
    }
}
