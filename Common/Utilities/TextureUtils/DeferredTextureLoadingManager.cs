using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Utilities.TextureUtils
{
    internal sealed class DeferredTextureLoadingManager : ModSystem
    {
        private static readonly List<IDeferredLoadTexture> Textures = [];
        private static readonly List<IDeferredLoadTexture> TexturesToRemove = [];
        private static bool hasItems;

        public static void Enqueue(IDeferredLoadTexture texture)
        {
            if (Main.dedServ || texture is null)
                return;

            Main.QueueMainThreadAction(() =>
            {
                Textures.Add(texture);
                hasItems = true;
            });
        }

        public override void Load()
        {
            Main.OnTickForThirdPartySoftwareOnly += UpdateDeferredTextures;
        }

        public override void Unload()
        {
            Main.OnTickForThirdPartySoftwareOnly -= UpdateDeferredTextures;
            Textures.Clear();
            TexturesToRemove.Clear();
            hasItems = false;
        }

        private static void UpdateDeferredTextures()
        {
            if (!hasItems)
                return;

            const int LimitPerTick = 2;
            int remaining = LimitPerTick;

            foreach (IDeferredLoadTexture texture in Textures)
            {
                if (texture.IsAssetLoaded)
                {
                    texture.OnTextureLoaded();
                    TexturesToRemove.Add(texture);
                    remaining--;
                }

                if (remaining <= 0)
                    break;
            }

            foreach (IDeferredLoadTexture texture in TexturesToRemove)
                Textures.Remove(texture);

            TexturesToRemove.Clear();
            hasItems = Textures.Count != 0;
        }
    }
}
