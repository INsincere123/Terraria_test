using Terraria;
using Terraria.ID;
using TestMod.Common.DynamicText;
using TestMod.Items.DamageTypes;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        private static void ApplyTrueDamageHitModifiers(Item item, ref NPC.HitModifiers modifiers)
        {
            if (!IsTrueDamageItem(item))
                return;

            modifiers.ScalingArmorPenetration += 1f;
            modifiers.HideCombatText();
        }

        private static void DrawTrueDamageCombatText(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!IsTrueDamageItem(item) || Main.netMode == NetmodeID.Server || player.whoAmI != Main.myPlayer)
                return;

            DynamicWorldTextSystem.SpawnCombatText(
                target.Hitbox,
                damageDone,
                hit.Crit,
                DynamicTextStyleRegistry.Get(DynamicTextStyleRegistry.DamageTrue).PrimaryColor,
                DynamicTextStyleRegistry.DamageTrue);
        }

        private static bool IsTrueDamageItem(Item item)
        {
            return item.DamageType == TrueDamageClass.Instance;
        }
    }
}
