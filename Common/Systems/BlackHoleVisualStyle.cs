using Microsoft.Xna.Framework.Graphics;

namespace TestMod.Common.Systems
{
    public enum BlackHoleCoreMode
    {
        Default,
        Texture
    }

    public enum BlackHoleDiskMode
    {
        Default,
        Material
    }

    public readonly struct BlackHoleVisualStyle
    {
        public static BlackHoleVisualStyle Default => new(BlackHoleCoreMode.Default, BlackHoleDiskMode.Default, null, null, null);

        public static BlackHoleVisualStyle DefaultCoreMaterialDisk
            => BlackHoleVisualAssetSystem.HasDiskMaterial
                ? new BlackHoleVisualStyle(BlackHoleCoreMode.Default, BlackHoleDiskMode.Material, null, null, null)
                : Default;

        public BlackHoleVisualStyle(
            BlackHoleCoreMode coreMode = BlackHoleCoreMode.Default,
            BlackHoleDiskMode diskMode = BlackHoleDiskMode.Default,
            Texture2D coreTexture = null,
            Texture2D diskTexture = null,
            Texture2D diskFlowTexture = null)
        {
            CoreMode = coreMode;
            DiskMode = diskMode;
            CoreTexture = coreTexture;
            DiskTexture = diskTexture;
            DiskFlowTexture = diskFlowTexture;
        }

        public BlackHoleCoreMode CoreMode { get; }

        public BlackHoleDiskMode DiskMode { get; }

        public Texture2D CoreTexture { get; }

        public Texture2D DiskTexture { get; }

        public Texture2D DiskFlowTexture { get; }

        public Texture2D GetRequestedCoreTexture()
            => CoreMode == BlackHoleCoreMode.Texture ? CoreTexture : null;

        public Texture2D GetRequestedDiskTexture()
            => DiskMode == BlackHoleDiskMode.Material ? DiskTexture : null;

        public Texture2D GetRequestedDiskFlowTexture()
            => DiskMode == BlackHoleDiskMode.Material ? DiskFlowTexture : null;
    }
}
