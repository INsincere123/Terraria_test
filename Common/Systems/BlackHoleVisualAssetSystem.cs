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
        public const string DiskTexturePath = "TestMod/Assets/Textures/Effects/BlackHoleDisk";
        public const string DiskFlowTexturePath = "TestMod/Assets/Textures/Effects/BlackHoleDiskFlow";

        private static Asset<Texture2D> coreTexture;
        private static Asset<Texture2D> diskTexture;
        private static Asset<Texture2D> diskFlowTexture;

        public static bool HasCoreTexture => coreTexture?.IsLoaded == true;
        public static bool HasDiskTexture => diskTexture?.IsLoaded == true;
        public static bool HasDiskFlowTexture => diskFlowTexture?.IsLoaded == true;
        public static bool HasDiskMaterial => HasDiskTexture && HasDiskFlowTexture;

        public static Texture2D CoreTexture => HasCoreTexture ? coreTexture.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D DiskTexture => HasDiskTexture ? diskTexture.Value : TextureAssets.MagicPixel.Value;
        public static Texture2D DiskFlowTexture => HasDiskFlowTexture ? diskFlowTexture.Value : TextureAssets.MagicPixel.Value;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            coreTexture = TryRequestTexture(CoreTexturePath);
            diskTexture = TryRequestTexture(DiskTexturePath);
            diskFlowTexture = TryRequestTexture(DiskFlowTexturePath);
        }

        public override void Unload()
        {
            coreTexture = null;
            diskTexture = null;
            diskFlowTexture = null;
        }

        private static Asset<Texture2D> TryRequestTexture(string path)
        {
            if (!ModContent.HasAsset(path))
                return null;

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
