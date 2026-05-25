using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Rarities;

namespace TestMod.Common.Systems
{
    public sealed class EventHorizonRaritySystem : ModSystem
    {
        public override void Load()
        {
            if (Main.dedServ)
                return;

            Main.QueueMainThreadAction(() =>
            {
                Main.graphics.GraphicsDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
                Main.graphics.ApplyChanges();
            });
        }

        public override void Unload()
        {
            if (Main.dedServ)
                return;

            EventHorizonRarity.UnloadResources();
        }
    }
}
