using Terraria;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalItems
{
    public partial class GlobalItem
    {
        public override void UseAnimation(Item item, Player player)
        {
            if (player.TryGetModPlayer(out TimeEchoPlayer echo)) echo.BeginMeleeSwing();
        }
    }
}
