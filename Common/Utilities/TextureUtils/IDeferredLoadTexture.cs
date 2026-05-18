namespace TestMod.Common.Utilities.TextureUtils
{
    public interface IDeferredLoadTexture
    {
        bool IsAssetLoaded { get; }

        void OnTextureLoaded();
    }
}
