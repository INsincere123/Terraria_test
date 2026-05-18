using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    public sealed class AntaresVisualAssetSystem : ModSystem
    {
        public const string HaloNoisePath = "TestMod/Assets/Textures/Effects/AntaresHaloNoise";
        public const string SpikeMaskPath = "TestMod/Assets/Textures/Effects/AntaresSpikeMask";
        public const string RadialRampPath = "TestMod/Assets/Textures/Effects/AntaresRadialRamp";
        public const string SoftStarMotePath = "TestMod/Assets/Textures/Effects/SoftStarMote";

        private static Asset<Texture2D> haloNoise;
        private static Asset<Texture2D> spikeMask;
        private static Asset<Texture2D> radialRamp;
        private static Asset<Texture2D> softStarMote;

        public static bool HasHaloNoise => haloNoise?.IsLoaded == true;
        public static bool HasSpikeMask => spikeMask?.IsLoaded == true;
        public static bool HasRadialRamp => radialRamp?.IsLoaded == true;
        public static bool HasSoftStarMote => softStarMote?.IsLoaded == true;

        public static Texture2D HaloNoise => HasHaloNoise ? haloNoise.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D SpikeMask => HasSpikeMask ? spikeMask.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D RadialRamp => HasRadialRamp ? radialRamp.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D SoftStarMote => HasSoftStarMote ? softStarMote.Value : TextureAssets.MagicPixel.Value;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            haloNoise = TryRequestTexture(HaloNoisePath);
            spikeMask = TryRequestTexture(SpikeMaskPath);
            radialRamp = TryRequestTexture(RadialRampPath);
            softStarMote = TryRequestTexture(SoftStarMotePath);
        }

        public override void Unload()
        {
            haloNoise = null;
            spikeMask = null;
            radialRamp = null;
            softStarMote = null;
        }

        private static Asset<Texture2D> TryRequestTexture(string path)
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
