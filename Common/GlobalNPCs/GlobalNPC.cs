using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => false;

        public override bool PreAI(NPC npc)
        {
            if (TimeStop_ShouldFreeze(npc))
            {
                TimeStop_FreezeNPC(npc);
                return false;
            }

            Blindness_PreAI(npc);
            return true;
        }

        public override void PostAI(NPC npc)
        {
            Blindness_PostAI(npc);
            TimeStop_PostAI(npc);
        }

        public override bool CheckDead(NPC npc)
        {
            if (TimeStop_ShouldFreeze(npc))
            {
                npc.life = 1;
                return false;
            }

            return true;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int CooldownSlot)
        {
            return !TimeStop_ShouldFreeze(npc);
        }

        public override bool? CanBeHitByItem(NPC npc, Player player, Item item)
        {
            if (TimeStop_ShouldFreeze(npc) && npc.life == 1)
                return false;

            return null;
        }

        public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
        {
            if (TimeStop_ShouldFreeze(npc) && npc.life == 1)
                return false;

            return null;
        }
    }
}
