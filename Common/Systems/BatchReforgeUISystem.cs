using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using TestMod.Common.UI;

namespace TestMod.Common.Systems
{
    public sealed class BatchReforgeUISystem : ModSystem
    {
        private UserInterface userInterface;

        public override void Load()
        {
            if (!Main.dedServ)
                userInterface = new UserInterface();
        }

        internal void Open(BatchReforgeKind kind)
        {
            if (userInterface == null || Main.LocalPlayer.dead ||
                userInterface.CurrentState is BatchReforgeUI current && current.Kind == kind)
                return;
            Main.playerInventory = true;
            Main.recBigList = false;
            userInterface.SetState(new BatchReforgeUI(kind));
            SoundEngine.PlaySound(SoundID.MenuOpen);
        }

        internal void Close() => userInterface?.SetState(null);

        public override void UpdateUI(GameTime gameTime)
        {
            if (userInterface?.CurrentState == null)
                return;
            if (!Main.playerInventory || Main.LocalPlayer.dead || !Main.LocalPlayer.active || Main.gameMenu)
            {
                Close();
                return;
            }
            userInterface.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (userInterface?.CurrentState == null)
                return;
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (index < 0)
                return;
            layers.Insert(index, new LegacyGameInterfaceLayer("TestMod: Batch Reforge", () =>
            {
                userInterface.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
                return true;
            }, InterfaceScaleType.UI));
        }

        public override void OnWorldUnload() => Close();

        public override void Unload()
        {
            Close();
            userInterface = null;
            BatchReforgeService.Unload();
        }
    }
}
