using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TestMod.Common.Players
{
    public class GodModePlayer : ModPlayer
    {
        internal const byte StatePacket = 16;
        public bool Enabled;
        private bool _receivedInitialState;

        // 在所有 ResetEffects 之后注入，避免召唤暴击标志被另一个 ModPlayer 清除。
        public override void PostUpdateEquips()
        {
            if (!Enabled) return;
            SummonCritPlayer.Enable(Player);
            Player.GetModPlayer<SummonCritPlayer>().BonusCritChance += 66f;
            Player.statDefense += 6666;
            Player.lifeRegen += 6666 * 2;
            Player.statLifeMax2 += 6666;
            Player.statManaMax2 += 666;
            Player.endurance = MathHelper.Clamp(Player.endurance + 0.66f, 0f, 0.999f);
            Player.maxMinions += 66;
            Player.maxTurrets += 33;
            Player.GetDamage(DamageClass.Generic) += 6.66f;
            Player.GetAttackSpeed(DamageClass.Generic) += 6.6f;
            Player.GetCritChance(DamageClass.Generic) += 66;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!Enabled) return;
            modifiers.ScalingArmorPenetration += 1f;
            modifiers.CritDamage += 1f;
        }

        // 新键有意不读取旧 godModeBuff / godModeUnlocked，旧角色首次默认关闭。
        public override void SaveData(TagCompound tag) => tag["godModeEnabled"] = Enabled;
        public override void LoadData(TagCompound tag) => Enabled = tag.GetBool("godModeEnabled");
        public override void Initialize() { Enabled = false; _receivedInitialState = false; }
        public override void CopyClientState(ModPlayer targetCopy) => ((GodModePlayer)targetCopy).Enabled = Enabled;
        public override void SendClientChanges(ModPlayer clientPlayer)
        {
            if (((GodModePlayer)clientPlayer).Enabled != Enabled) SendState();
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => SendState(toWho, fromWho);

        private void SendState(int toWho = -1, int ignore = -1)
        {
            if (Main.netMode == NetmodeID.SinglePlayer ||
                Main.netMode == NetmodeID.Server && !_receivedInitialState) return;
            var packet = Mod.GetPacket();
            packet.Write(StatePacket);
            packet.Write((byte)Player.whoAmI);
            packet.Write(Enabled);
            packet.Send(toWho, ignore);
        }

        internal static void ReceiveState(BinaryReader reader, int sender)
        {
            int index = reader.ReadByte();
            bool enabled = reader.ReadBoolean();
            if (index >= Main.maxPlayers ||
                Main.netMode == NetmodeID.Server && index != sender ||
                Main.netMode == NetmodeID.MultiplayerClient && index == Main.myPlayer) return;
            Player player = Main.player[index];
            if (!player.active || !player.TryGetModPlayer(out GodModePlayer state)) return;
            state.Enabled = enabled;
            state._receivedInitialState = true;
            if (Main.netMode == NetmodeID.Server) state.SendState(ignore: sender);
        }
    }
}
