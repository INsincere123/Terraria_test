using Terraria;
using TestMod.Items.DamageTypes;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        private static void ApplyTrueDamageHitModifiers(Item item, ref NPC.HitModifiers modifiers)
        {
            if (item.DamageType == TrueDamageClass.Instance)
                modifiers.ScalingArmorPenetration += 1f;
        }
    }
}
