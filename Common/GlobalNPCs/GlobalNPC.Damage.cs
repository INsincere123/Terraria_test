using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.ArmorShred;
using TestMod.Content.Items.DamageTypes;
using TestMod.Content.Buffs;

namespace TestMod.Common.GlobalNPCs
{
    public partial class GlobalNPC
    {
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            int stacks = ArmorShredSystem.GetStacks(npc.whoAmI);
            if (stacks > 0)
                modifiers.Defense.Flat -= stacks * ArmorShredSystem.DefensePerStack;

            if (modifiers.DamageType == TrueDamageClass.Instance)
                modifiers.ScalingArmorPenetration += 1f;
        }

        private static void ClearArmorShredIfExpired(NPC npc)
        {
            if (!npc.HasBuff(ModContent.BuffType<ArmorShredDebuff>()))
                ArmorShredSystem.Remove(npc.whoAmI);
        }
    }
}
