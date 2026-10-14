using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.IO;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalNPCs
{
    public sealed class DecoyState : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        internal EnemySource ParentSource;
        internal bool ParentFromSpawn;
        internal int RegisteredType;
        internal readonly DecoySelection Selection = new();
        public override void SetDefaults(NPC npc)
        {
            ParentSource = default; ParentFromSpawn = false; RegisteredType = npc.type;
            Selection.Reset();
        }
        public override void OnKill(NPC npc) => SetDefaults(npc);
        internal void BindParent(NPC npc, IEntitySource source)
        {
            if (npc.boss || !DecoySystem.Eligible(npc)) return;
            ParentSource = DecoySystem.SourceForSpawn(source);
            ParentFromSpawn = ParentSource.Valid;
        }
        internal NPC GetTarget(NPC npc)
        {
            NPC decoy = Selection.Get(npc, out bool changed);
            if (changed) npc.netUpdate = true;
            return decoy;
        }
        public override void SendExtraAI(NPC npc, BitWriter bits, BinaryWriter writer)
        {
            bits.WriteBit(ParentSource.Valid);
            if (ParentSource.Valid)
            {
                bits.WriteBit(ParentFromSpawn);
                writer.Write((short)ParentSource.Slot); writer.Write(ParentSource.Type); writer.Write(ParentSource.Generation);
            }
            Selection.Target.Write(writer);
        }
        public override void ReceiveExtraAI(NPC npc, BitReader bits, BinaryReader reader)
        {
            EnemySource parent = default;
            bool fromSpawn = false;
            if (bits.ReadBit())
            {
                fromSpawn = bits.ReadBit();
                parent = new(reader.ReadInt16(), reader.ReadInt32(), reader.ReadUInt32());
            }
            DecoyHandle target = DecoyHandle.Read(reader);
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            ParentSource = parent.Valid ? parent : default;
            ParentFromSpawn = parent.Valid && fromSpawn && !npc.boss;
            Selection.Inherit(target);
        }
    }
}
