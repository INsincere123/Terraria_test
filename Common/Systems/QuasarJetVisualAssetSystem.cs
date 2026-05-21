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
        public const string BeamBodyTexturePath = "TestMod/Assets/Textures/Effects/QuasarBeamBody";
        public const string BeamFlowTexturePath = "TestMod/Assets/Textures/Effects/QuasarBeamFlow";
        public const string BeamCoreTexturePath = "TestMod/Assets/Textures/Effects/QuasarBeamCore";

        private static Asset<Texture2D> headTexture;
        private static Asset<Texture2D> trailTexture;
        private static Asset<Texture2D> glowTexture;
        private static Asset<Texture2D> beamBodyTexture;
        private static Asset<Texture2D> beamFlowTexture;
        private static Asset<Texture2D> beamCoreTexture;

        public static Texture2D HeadTexture => headTexture?.IsLoaded == true ? headTexture.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D TrailTexture => trailTexture?.IsLoaded == true ? trailTexture.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D GlowTexture => glowTexture?.IsLoaded == true ? glowTexture.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D BeamBodyTexture => beamBodyTexture?.IsLoaded == true ? beamBodyTexture.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D BeamFlowTexture => beamFlowTexture?.IsLoaded == true ? beamFlowTexture.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D BeamCoreTexture => beamCoreTexture?.IsLoaded == true ? beamCoreTexture.Value : TextureAssets.MagicPixel.Value;

        public static bool HasHeadTexture => headTexture?.IsLoaded == true;
        public static bool HasTrailTexture => trailTexture?.IsLoaded == true;
        public static bool HasGlowTexture => glowTexture?.IsLoaded == true;
        public static bool HasBeamBodyTexture => beamBodyTexture?.IsLoaded == true;
        public static bool HasBeamFlowTexture => beamFlowTexture?.IsLoaded == true;
        public static bool HasBeamCoreTexture => beamCoreTexture?.IsLoaded == true;
        public static bool HasFineBeamMaterial => HasBeamBodyTexture || HasBeamFlowTexture || HasBeamCoreTexture;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            headTexture = TryRequest(HeadTexturePath);
            trailTexture = TryRequest(TrailTexturePath);
            glowTexture = TryRequest(GlowTexturePath);
            beamBodyTexture = TryRequest(BeamBodyTexturePath);
            beamFlowTexture = TryRequest(BeamFlowTexturePath);
            beamCoreTexture = TryRequest(BeamCoreTexturePath);
        }

        public override void Unload()
        {
            headTexture = null;
            trailTexture = null;
            glowTexture = null;
            beamBodyTexture = null;
            beamFlowTexture = null;
            beamCoreTexture = null;
        }

        private static Asset<Texture2D> TryRequest(string path)
        {
            try
            {
                if (!ModContent.HasAsset(path))
                    return null;

                return ModContent.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad);
            }
            catch
            {
                return null;
            }
        }
    }
}
