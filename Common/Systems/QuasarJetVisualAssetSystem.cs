using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public sealed class QuasarJetVisualAssetSystem : ModSystem
    {
        public const string HeadTexturePath = "TestMod/Assets/Textures/Effects/QuasarJetHead";
        public const string TrailTexturePath = "TestMod/Assets/Textures/Effects/QuasarJetTrail";
        public const string GlowTexturePath = "TestMod/Assets/Textures/Effects/QuasarJetGlow";

        private static Asset<Texture2D> headTexture;
        private static Asset<Texture2D> trailTexture;
        private static Asset<Texture2D> glowTexture;

        public static Texture2D HeadTexture => headTexture?.IsLoaded == true ? headTexture.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D TrailTexture => trailTexture?.IsLoaded == true ? trailTexture.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D GlowTexture => glowTexture?.IsLoaded == true ? glowTexture.Value : TextureAssets.MagicPixel.Value;

        public static bool HasHeadTexture => headTexture?.IsLoaded == true;
        public static bool HasTrailTexture => trailTexture?.IsLoaded == true;
        public static bool HasGlowTexture => glowTexture?.IsLoaded == true;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            headTexture = TryRequest(HeadTexturePath);
            trailTexture = TryRequest(TrailTexturePath);
            glowTexture = TryRequest(GlowTexturePath);
        }

        public override void Unload()
        {
            headTexture = null;
            trailTexture = null;
            glowTexture = null;
        }

        private static Asset<Texture2D> TryRequest(string path)
        {
            try
            {
                return ModContent.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad);
            }
            catch
            {
                return null;
            }
        }
    }
}
