using Terraria;
using Terraria.ModLoader;
using TestMod.Common.GlobalNPCs;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalItems
{
    public sealed class LifetakingGlobalItem : Terraria.ModLoader.GlobalItem
    {
        public override void UseAnimation(Item item, Player player)
            => player.GetModPlayer<LifetakingPlayer>().BeginSwing();

        public override bool? CanHitNPC(Item item, Player player, NPC target)
        {
            if (LifetakingAttackSystem.IsVirtualMelee(player, item, target))
                return LifetakingAttackSystem.EnterVirtualMelee(player, item, target) ? null : false;
            return player.GetModPlayer<LifetakingPlayer>().MeleeLedger.Contains(target.whoAmI,
                target.GetGlobalNPC<HookTargetIdentity>().Generation, 0) ? false : null;
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (damageDone > 0 && LifetakingAttackSystem.IsVirtualMelee(player, item, target))
                player.GetModPlayer<LifetakingPlayer>().Pulse();
        }
    }
}
