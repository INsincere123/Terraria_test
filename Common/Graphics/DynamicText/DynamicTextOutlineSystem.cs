using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Graphics.DynamicText
{
    // 描边仅在帧前生成；文字绘制入口不切换 RenderTarget 或 SpriteBatch。
    public sealed class DynamicTextOutlineSystem : ModSystem
    {
        private const string ShaderPath = "Assets/Effects/DynamicTextOutlineShader.fxc";
        private const int MaxPendingMasks = 32;
        private const int MasksPerFrame = 4;
        private const long MaxMaskBytes = 8 * 1024 * 1024;
        private static readonly DynamicTextCache<MaskKey, RenderTarget2D> Masks = new(16, MaxMaskBytes, RetireMask);
        private static readonly HashSet<MaskKey> Pending = [];
        private static readonly MaskKey[] requests = new MaskKey[MasksPerFrame];
        private static readonly List<Texture2D> Retired = [];
        private static SpriteBatch maskBatch;
        private static Effect shader;
        private static bool active;
        private static bool failed;
        private static bool retirementQueued;

        private readonly record struct MaskKey(Texture2D Source, int RadiusX, int RadiusY);

        public override void Load()
        {
            if (Main.dedServ)
                return;
            active = true;
            RenderTargetManager.RenderTargetUpdateLoopEvent += PrepareMasks;
        }

        internal static bool TryGetMask(Texture2D source, float radius, Vector2 scale, out Texture2D mask)
        {
            mask = null;
            if (!active || failed || source is null || source.IsDisposed || scale.X <= 0f || scale.Y <= 0f)
                return false;
            // 无法留在缓存中的遮罩直接回退，避免每帧生成后立即淘汰。
            if ((long)source.Width * source.Height * 4 > MaxMaskBytes)
                return false;
            // 以 1/4 像素归档，避免世界文字的连续缩放每帧制造一个新遮罩。
            float localX = radius / scale.X;
            float localY = radius / scale.Y;
            if (localX > 4f || localY > 4f || localX <= 0f || localY <= 0f)
                return false;
            MaskKey key = new(source, Math.Clamp((int)MathF.Round(localX * 4f), 1, 16), Math.Clamp((int)MathF.Round(localY * 4f), 1, 16));
            if (Masks.TryGetValue(key, out RenderTarget2D cached) && !cached.IsDisposed)
            {
                mask = cached;
                return true;
            }
            if (Pending.Count < MaxPendingMasks)
                Pending.Add(key);
            return false;
        }

        private static void PrepareMasks()
        {
            if (!active || failed || Pending.Count == 0 || Main.dedServ)
                return;
            GraphicsDevice device = Main.graphics.GraphicsDevice;
            RenderTargetBinding[] targets = device.GetRenderTargets();
            Viewport viewport = device.Viewport;
            Rectangle scissor = device.ScissorRectangle;
            BlendState blend = device.BlendState;
            DepthStencilState depth = device.DepthStencilState;
            RasterizerState rasterizer = device.RasterizerState;
            SamplerState sampler = device.SamplerStates[0];
            try
            {
                shader ??= new Effect(device, ModContent.GetInstance<global::TestMod.TestMod>().GetFileBytes(ShaderPath));
                maskBatch ??= new SpriteBatch(device);
                // 先取出有限请求，避免修改正在枚举的集合，也限制首次生成的帧开销。
                int requestCount = 0;
                foreach (MaskKey key in Pending)
                {
                    requests[requestCount++] = key;
                    if (requestCount >= MasksPerFrame)
                        break;
                }
                for (int i = 0; i < requestCount; i++)
                {
                    MaskKey key = requests[i];
                    Pending.Remove(key);
                    if (key.Source.IsDisposed)
                        continue;
                    if (Masks.TryGetValue(key, out RenderTarget2D existing) && !existing.IsDisposed)
                        continue;
                    BuildMask(device, key);
                }
            }
            catch (Exception exception)
            {
                failed = true;
                Pending.Clear();
                ModContent.GetInstance<global::TestMod.TestMod>().Logger.Warn($"Failed to prepare dynamic text outlines; using sprite outlines: {exception}");
            }
            finally
            {
                Array.Clear(requests);
                device.SetRenderTargets(targets);
                device.Viewport = viewport;
                device.ScissorRectangle = scissor;
                device.BlendState = blend;
                device.DepthStencilState = depth;
                device.RasterizerState = rasterizer;
                device.SamplerStates[0] = sampler;
            }
        }

        private static void BuildMask(GraphicsDevice device, MaskKey key)
        {
            RenderTarget2D target = new(device, key.Source.Width, key.Source.Height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            bool keep = false;
            try
            {
                shader.Parameters["sampleOffset"].SetValue(new Vector2(key.RadiusX * 0.25f / key.Source.Width, key.RadiusY * 0.25f / key.Source.Height));
                device.SetRenderTarget(target);
                device.Clear(Color.Transparent);
                maskBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, shader);
                try
                {
                    maskBatch.Draw(key.Source, Vector2.Zero, Color.White);
                }
                finally
                {
                    maskBatch.End();
                }
                Masks.Add(key, target, (long)target.Width * target.Height * 4);
                keep = true;
            }
            finally
            {
                if (!keep)
                    RetireMask(target);
            }
        }

        private static void RetireMask(RenderTarget2D target)
        {
            if (target is null || target.IsDisposed)
                return;
            Retired.Add(target);
            if (retirementQueued)
                return;
            retirementQueued = true;
            Main.QueueMainThreadAction(() =>
            {
                foreach (Texture2D texture in Retired)
                    if (!texture.IsDisposed)
                        texture.Dispose();
                Retired.Clear();
                retirementQueued = false;
            });
        }

        public override void Unload()
        {
            active = false;
            if (!Main.dedServ)
                RenderTargetManager.RenderTargetUpdateLoopEvent -= PrepareMasks;
            Pending.Clear();
            Array.Clear(requests);
            List<Texture2D> textures = new(Retired);
            textures.AddRange(Masks.Values);
            Retired.Clear();
            Masks.Clear();
            SpriteBatch oldBatch = maskBatch;
            Effect oldShader = shader;
            maskBatch = null;
            shader = null;
            failed = false;
            if (textures.Count > 0 || oldBatch is not null || oldShader is not null)
                Main.QueueMainThreadAction(() =>
                {
                    foreach (Texture2D texture in textures)
                        if (!texture.IsDisposed)
                            texture.Dispose();
                    oldBatch?.Dispose();
                    oldShader?.Dispose();
                });
        }
    }
}
