using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TestMod.Common.GlobalNPCs
{
    // NPC 槽位可复用，同类型的新 NPC 也不能继承旧钩点。
    public class HookTargetIdentity : Terraria.ModLoader.GlobalNPC
    {
        private static uint _nextGeneration;
        public uint Generation { get; private set; }
        public override bool InstancePerEntity => true;
        public override void Unload() => _nextGeneration = 0;

        public override void SetDefaults(NPC npc)
        {
            Generation = Main.netMode == NetmodeID.MultiplayerClient ? 0 : ++_nextGeneration;
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
            => binaryWriter.Write(Generation);

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
            => Generation = binaryReader.ReadUInt32();
    }
}
