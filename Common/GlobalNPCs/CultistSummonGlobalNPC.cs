using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalNPCs
{
    // 独立于共享 GlobalNPC：重战标记只属于本次召唤的邪教徒实体。
    public class CultistSummonGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.CultistBoss;

        internal bool SuppressPillars;
        internal bool IsItemSummoned;

        public override void SetDefaults(NPC npc)
        {
            SuppressPillars = false;
            IsItemSummoned = false;
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(IsItemSummoned);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            IsItemSummoned = bitReader.ReadBit();
            CultistSummonSystem.RefreshRitualProtection();
        }
    }
}
