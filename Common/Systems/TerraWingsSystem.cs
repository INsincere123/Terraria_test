using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    public class TerraWingsSystem : ModSystem
    {
        public override void OnWorldUnload()
        {
            foreach (Player player in Main.player)
                if (player != null && player.TryGetModPlayer(out TerraWingsPlayer state)) state.ResetSession();
        }
    }
}
