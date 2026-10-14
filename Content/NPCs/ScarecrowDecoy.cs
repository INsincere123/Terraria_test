using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Content.NPCs
{
    public sealed class ScarecrowDecoy : ModNPC
    {
        internal const int BodyWidth = 32, BodyHeight = 64;
        internal int Owner = -1, Remaining;
        private ulong lastTick = ulong.MaxValue;
        internal bool IsAlive => Remaining > 0 && Owner >= 0 && Owner < Main.maxPlayers &&
            Main.player[Owner] is { active: true, dead: false };
        public override string Texture => "Terraria/Images/NPC_" + NPCID.TargetDummy;
        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.TargetDummy];
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new() { Hide = true });
        }
        public override void SetDefaults()
        {
            NPC.width = BodyWidth; NPC.height = BodyHeight;
            NPC.lifeMax = 1; NPC.damage = 0; NPC.defense = 0;
            NPC.friendly = true; NPC.dontTakeDamage = true; NPC.immortal = true;
            NPC.noGravity = true; NPC.noTileCollide = true; NPC.knockBackResist = 0;
            NPC.aiStyle = -1; NPC.npcSlots = 0; NPC.chaseable = false; NPC.value = 0;
            NPC.dontCountMe = true; NPC.netAlways = true; NPC.ShowNameOnHover = false;
            Owner = -1; Remaining = 0; lastTick = ulong.MaxValue;
        }
        internal void Initialize(int owner)
        {
            Owner = owner; Remaining = ScarecrowSystem.LifetimeTicks; lastTick = Main.GameUpdateCount;
            ScarecrowSystem.Register(NPC);
        }
        internal void Despawn()
        {
            ScarecrowSystem.Remove(DecoyHandle.Of(NPC));
            Remaining = 0; NPC.active = false;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
        }
        public override void AI()
        {
            NPC.velocity = Vector2.Zero; NPC.timeLeft = 2;
            TickLifetime();
            if (IsAlive) ScarecrowSystem.Register(NPC);
        }
        internal void TickLifetime()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (lastTick != Main.GameUpdateCount)
                {
                    lastTick = Main.GameUpdateCount;
                    if (Remaining > 0) Remaining--;
                }
                if (!IsAlive) { Despawn(); return; }
                if (Remaining % 60 == 0) NPC.netUpdate = true;
            }
        }
        public override bool CheckActive() => false;
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
        public override bool CanHitNPC(NPC target) => false;
        public override bool PreKill() => false;
        public override void OnKill() => ScarecrowSystem.Remove(DecoyHandle.Of(NPC));
        public override void SendExtraAI(BinaryWriter writer) { writer.Write((short)Owner); writer.Write((short)Remaining); }
        public override void ReceiveExtraAI(BinaryReader reader)
        {
            int owner = reader.ReadInt16(), remaining = reader.ReadInt16();
            if (Main.netMode != NetmodeID.MultiplayerClient) return;
            Owner = owner >= 0 && owner < Main.maxPlayers ? owner : -1;
            Remaining = remaining >= 0 && remaining <= ScarecrowSystem.LifetimeTicks ? remaining : 0;
        }
    }
}
