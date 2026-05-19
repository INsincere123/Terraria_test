using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public sealed class BlackHoleVisualAssetSystem : ModSystem
    {
        public const string CoreTexturePath = "TestMod/Assets/Textures/Effects/BlackHoleCore";

        private static Asset<Texture2D> coreTexture;

        public static bool HasCoreTexture => coreTexture?.IsLoaded == true;

        public static Texture2D CoreTexture => HasCoreTexture ? coreTexture.Value : TextureAssets.MagicPixel.Value;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            try
            {
                coreTexture = ModContent.Request<Texture2D>(CoreTexturePath, AssetRequestMode.ImmediateLoad);
            }
            catch
            {
                coreTexture = null;
            }
        }

        public override void Unload()
        {
            coreTexture = null;
        }
    }
}
