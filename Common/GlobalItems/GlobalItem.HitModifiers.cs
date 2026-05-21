using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            ApplyTrueDamageHitModifiers(item, ref modifiers);
            ApplyBloodFeedHitModifiers(player, ref modifiers);
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            DrawTrueDamageCombatText(item, player, target, hit, damageDone);
        }
    }
}
