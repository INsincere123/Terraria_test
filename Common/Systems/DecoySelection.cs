using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TestMod.Common.GlobalNPCs;
using TestMod.Content.NPCs;

namespace TestMod.Common.Systems
{
    internal readonly record struct DecoyHandle(int Slot, uint Generation)
    {
        internal bool Valid => Generation != 0 && Slot >= 0 && Slot < Main.maxNPCs;
        internal NPC Resolve()
        {
            if (!Valid) return null;
            NPC npc = Main.npc[Slot];
            return npc is { active: true, ModNPC: ScarecrowDecoy decoy } && decoy.IsAlive &&
                npc.GetGlobalNPC<HookTargetIdentity>().Generation == Generation ? npc : null;
        }
        internal static DecoyHandle Of(NPC npc) => new(npc.whoAmI, npc.GetGlobalNPC<HookTargetIdentity>().Generation);
        internal void Write(BinaryWriter writer)
        {
            writer.Write((short)(Valid ? Slot : -1));
            if (Valid) writer.Write(Generation);
        }
        internal static DecoyHandle Read(BinaryReader reader)
        {
            int slot = reader.ReadInt16();
            DecoyHandle value = slot < 0 ? default : new(slot, reader.ReadUInt32());
            return value.Valid ? value : default;
        }
    }

    // 每实体只保存选择结果；正常 AI 不遍历玩家或全部 NPC。
    internal sealed class DecoySelection
    {
        internal DecoyHandle Target;
        private uint revision;
        private ulong nextSearch;
        private bool initialized;

        internal NPC Get(Entity entity, out bool changed)
        {
            changed = false;
            if (Main.netMode != NetmodeID.MultiplayerClient &&
                (!initialized || revision != ScarecrowSystem.Revision || Main.GameUpdateCount >= nextSearch))
            {
                initialized = true; revision = ScarecrowSystem.Revision;
                nextSearch = Main.GameUpdateCount + ScarecrowSystem.RetargetTicks;
                DecoyHandle next = ScarecrowSystem.FindNearest(entity.Center);
                changed = Target != next; Target = next;
            }
            NPC decoy = Target.Resolve();
            // 超出范围当次 AI 即停止；不每帧扫描来寻找替代目标。
            return decoy != null && Vector2.DistanceSquared(entity.Center, decoy.Center) <= ScarecrowSystem.RangeSquared
                ? decoy : null;
        }

        internal void Reset()
        {
            Target = default; initialized = false; revision = 0; nextSearch = 0;
        }

        internal void Inherit(DecoyHandle handle)
        {
            Target = handle; initialized = handle.Valid;
            revision = ScarecrowSystem.Revision;
            nextSearch = Main.GameUpdateCount + ScarecrowSystem.RetargetTicks;
        }
    }
}
