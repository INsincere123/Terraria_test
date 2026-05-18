using Terraria;
using TestMod.Common.Mechanics.ArmorShred;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC
    {
        public override void OnKill(NPC npc)
        {
            ArmorShredSystem.Remove(npc.whoAmI);
            CollectorSystem.OnNpcKilled(npc);
            ApplyArenaAltarKillEffects(npc);
        }

        private static void ApplyArenaAltarKillEffects(NPC npc)
        {
            if (!ArenaAltarSystem.IsInActiveRange(npc.Center))
                return;

            if (npc.friendly && !npc.boss)
                ArenaAltarSystem.GrantLifestealToNearby();
            else if (!npc.friendly && !npc.townNPC)
                ArenaAltarSystem.GrantDamageBoostToNearby();
        }
    }
}
