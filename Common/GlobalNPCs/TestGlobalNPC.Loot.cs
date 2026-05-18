using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Items.Accessories;
using TestMod.Items.Weapons.Melee;

namespace TestMod.Common.GlobalNPCs
{
    public partial class TestGlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.HallowBoss)
            {
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SwordQiSword>(), 4));

                var rule = new LeadingConditionRule(new Conditions.EmpressOfLightIsGenuinelyEnraged());
                rule.OnSuccess(ItemDropRule.Common(ModContent.ItemType<HeavenlyCrown>()));
                npcLoot.Add(rule);
            }

            if (npc.type == NPCID.Deerclops)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<AshenSeal>(), 4));

            if (NPCID.Sets.DemonEyes[npc.type] || NPCID.Sets.Zombies[npc.type])
                npcLoot.Add(ItemDropRule.Common(ItemID.AbigailsFlower, 15));
        }
    }
}
