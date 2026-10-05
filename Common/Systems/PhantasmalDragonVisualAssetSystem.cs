using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Systems
{
    /// <summary>绘制线程首次生成反色纹理，后续两条链共用原图和缓存，不切换 SpriteBatch。</summary>
    public sealed class PhantasmalDragonVisualAssetSystem : ModSystem
    {
        private static Texture2D invertedTexture;

        internal static Texture2D GetInvertedTexture(Texture2D original)
        {
            if (Main.dedServ) return original;
            if (invertedTexture != null && !invertedTexture.IsDisposed) return invertedTexture;

            Color[] pixels = new Color[original.Width * original.Height];
            original.GetData(pixels);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = InvertPremultiplied(pixels[i]);

            Texture2D generated = new Texture2D(original.GraphicsDevice, original.Width, original.Height);
            try
            {
                generated.SetData(pixels);
                invertedTexture = generated;
            }
            catch
            {
                generated.Dispose();
                throw;
            }
            return invertedTexture;
        }

        // 原图已预乘 alpha：(1 - RGB / A) * A = A - RGB。
        // alpha=0 时 RGB 也保持0，不会给透明边缘填入白色。
        internal static Color InvertPremultiplied(Color color) => new Color(
            Math.Max(0, color.A - color.R),
            Math.Max(0, color.A - color.G),
            Math.Max(0, color.A - color.B),
            (int)color.A);

        public override void Unload()
        {
            Texture2D toDispose = invertedTexture;
            invertedTexture = null;
            if (toDispose != null)
                Main.QueueMainThreadAction(() => toDispose.Dispose());
        }
    }
}
