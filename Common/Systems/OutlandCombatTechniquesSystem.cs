using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.GlobalNPCs;
using TestMod.Content.Items;

namespace TestMod.Common.Systems
{
    public sealed class OutlandCombatTechniquesSystem : ModSystem
    {
        internal const byte UseRequestPacket = 12;
        internal static bool VolumeOneUsed { get; private set; }
        internal static bool VolumeTwoUsed { get; private set; }
        internal static bool IsUsed(byte volume) => volume == 1 ? VolumeOneUsed : VolumeTwoUsed;

        public override void ClearWorld() => VolumeOneUsed = VolumeTwoUsed = false;
        public override void OnWorldUnload() => ClearWorld();
        public override void Unload() => ClearWorld();

        public override void SaveWorldData(TagCompound tag)
        {
            if (VolumeOneUsed) tag["OutlandCombatVolumeOne"] = true;
            if (VolumeTwoUsed) tag["OutlandCombatVolumeTwo"] = true;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            VolumeOneUsed = tag.GetBool("OutlandCombatVolumeOne");
            VolumeTwoUsed = tag.GetBool("OutlandCombatVolumeTwo");
            // 原版及模组 NPC 均已读取，当前生命仍按未强化的上限初始化。
            ApplyLifeBonus(initializeLife: true);
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(VolumeOneUsed);
            writer.Write(VolumeTwoUsed);
        }

        public override void NetReceive(BinaryReader reader)
        {
            VolumeOneUsed = reader.ReadBoolean();
            VolumeTwoUsed = reader.ReadBoolean();
            ApplyLifeBonus();
        }

        internal static void RequestUse(Player player, byte volume)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = ModContent.GetInstance<OutlandCombatTechniquesSystem>().Mod.GetPacket();
                packet.Write(UseRequestPacket);
                packet.Write(volume);
                packet.Write((byte)player.selectedItem);
                packet.Send();
            }
            else
                TryUse(player, volume, player.selectedItem);
        }

        internal static void ReceiveUseRequest(BinaryReader reader, int sender)
        {
            byte volume = reader.ReadByte();
            int slot = reader.ReadByte();
            if (Main.netMode == NetmodeID.Server && sender >= 0 && sender < Main.maxPlayers)
                TryUse(Main.player[sender], volume, slot);
        }

        private static void TryUse(Player player, byte volume, int slot)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || volume < 1 || volume > 2 ||
                IsUsed(volume) || !player.active || player.dead || slot != player.selectedItem ||
                slot < 0 || slot >= player.inventory.Length ||
                player.inventory[slot].ModItem is not OutlandCombatTechniquesItem book ||
                book.Volume != volume || player.inventory[slot].stack <= 0)
                return;

            if (volume == 1) VolumeOneUsed = true;
            else VolumeTwoUsed = true;
            Item item = player.inventory[slot];
            if (--item.stack <= 0) item.TurnToAir();
            ApplyLifeBonus();

            string key = $"Mods.TestMod.OutlandCombatTechniques.Volume{volume}Used";
            if (Main.netMode == NetmodeID.Server)
            {
                NetMessage.SendData(MessageID.SyncEquipment, number: player.whoAmI, number2: slot);
                NetMessage.SendData(MessageID.WorldData);
                ChatHelper.BroadcastChatMessage(NetworkText.FromKey(key), Color.Gold);
            }
            else
                Main.NewText(Language.GetTextValue(key), Color.Gold);
        }

        private static void ApplyLifeBonus(bool initializeLife = false)
        {
            if (!VolumeTwoUsed) return;
            foreach (NPC npc in Main.ActiveNPCs)
                if (npc.TryGetGlobalNPC(out OutlandCombatTechniquesGlobalNPC state))
                    state.ApplyLifeBonus(npc, initializeLife);
        }
    }
}
