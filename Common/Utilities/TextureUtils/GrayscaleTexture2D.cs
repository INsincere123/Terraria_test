using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Utilities.TextureUtils
{
    public sealed class GrayscaleTexture2D : IDeferredLoadTexture
    {
        private int width;
        private int height;
        private float[] scales;
        private Asset<Texture2D> asset;
        private bool prepared;

        public Texture2D Texture { get; private set; }

        public bool IsPrepared => prepared;

        public bool IsAssetLoaded => asset?.IsLoaded ?? false;

        public GrayscaleTexture2D(string assetName)
        {
            if (Main.dedServ)
                return;

            asset = ModContent.Request<Texture2D>(assetName);
            Texture = asset.Value;
            DeferredTextureLoadingManager.Enqueue(this);
        }

        public void Unload()
        {
            width = 0;
            height = 0;
            scales = null;
            asset = null;
            Texture = null;
            prepared = false;
        }

        public void OnTextureLoaded()
        {
            if (prepared)
                return;

            Texture = asset.Value;
            if (Texture is null)
                return;

            width = Texture.Width;
            height = Texture.Height;
            scales = new float[width * height];

            Color[] colors = new Color[width * height];
            Texture.GetData(colors);

            for (int i = 0; i < colors.Length; i++)
                scales[i] = colors[i].R / 255f;

            prepared = true;
        }

        public float GetClamp(int x, int y)
        {
            if (!prepared || width <= 0 || height <= 0)
                return 0f;

            x = Math.Clamp(x, 0, width - 1);
            y = Math.Clamp(y, 0, height - 1);
            return scales[x + y * width];
        }

        public float GetRepeat(int x, int y)
        {
            if (!prepared || width <= 0 || height <= 0)
                return 0f;

            x %= width;
            y %= height;
            if (x < 0)
                x += width;
            if (y < 0)
                y += height;

            return scales[x + y * width];
        }
    }
}
