using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TestMod.Common.Systems
{
    public readonly struct BlackHoleLensRequest
    {
        public readonly Vector2 WorldCenter;
        public readonly float Radius;
        public readonly float Opacity;
        public readonly Color AccretionColor;
        public readonly BlackHoleVisualStyle VisualStyle;

        public BlackHoleLensRequest(Vector2 worldCenter, float radius, float opacity, Color accretionColor, BlackHoleVisualStyle visualStyle)
        {
            WorldCenter = worldCenter;
            Radius = radius;
            Opacity = opacity;
            AccretionColor = accretionColor;
            VisualStyle = visualStyle;
        }
    }

    public sealed class ShaderRenderTargetSystem : ModSystem
    {
        public const string CompositeShaderPath = "Assets/Effects/ShaderRenderTargetComposite.fxc";
        private const int MaxFrameFlares = 48;
        private const int MaxFrameBlackHoles = 8;

        private static readonly List<StarFlareRequest> starFlares = [];
        private static readonly List<BlackHoleLensRequest> blackHoleLenses = [];

        private static RenderTarget2D overlayTarget;
        private static Effect compositeShader;
        private static bool loggedShaderLoadFailure;

        public static void RegisterBlackHoleLens(BlackHoleLensRequest request)
        {
            if (Main.dedServ || request.Radius <= 0f || request.Opacity <= 0f)
                return;

            GravitationalLensSystem.RegisterBlackHole(request.WorldCenter, request.Radius, request.Opacity, request.AccretionColor, request.VisualStyle);

            if (blackHoleLenses.Count < MaxFrameBlackHoles)
                blackHoleLenses.Add(request);
        }

        public static void RegisterAntaresStarFlare(Vector2 worldCenter, float radius, float intensity, int tier)
        {
            if (Main.dedServ || radius <= 0f || intensity <= 0f || starFlares.Count >= MaxFrameFlares)
                return;

            starFlares.Add(new StarFlareRequest(worldCenter, radius, MathHelper.Clamp(intensity, 0f, 2f), tier));
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
            if (index < 0)
                index = layers.Count;

            layers.Insert(index, new LegacyGameInterfaceLayer("TestMod: Shader Render Targets", DrawCompositeLayer, InterfaceScaleType.Game));
        }

        public override void Unload()
        {
            starFlares.Clear();
            blackHoleLenses.Clear();

            RenderTarget2D oldTarget = overlayTarget;
            Effect oldShader = compositeShader;
            overlayTarget = null;
            compositeShader = null;
            loggedShaderLoadFailure = false;

            if (Main.dedServ || oldTarget is null && oldShader is null)
                return;

            Main.QueueMainThreadAction(() =>
            {
                oldTarget?.Dispose();
                oldShader?.Dispose();
            });
        }

        private static bool DrawCompositeLayer()
        {
            if (Main.dedServ)
                return true;

            if (starFlares.Count <= 0 && blackHoleLenses.Count <= 0)
                return true;

            DrawOverlayDirect();

            starFlares.Clear();
            blackHoleLenses.Clear();
            return true;
        }

        private static void DrawOverlayDirect()
        {
            SpriteBatch spriteBatch = Main.spriteBatch;

            spriteBatch.End();
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Matrix.Identity);

            DrawBlackHoleLensMasks(spriteBatch);
            DrawAntaresFlareMasks(spriteBatch);

            spriteBatch.End();
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                Main.Rasterizer,
                null,
                Main.UIScaleMatrix);
        }

        private static void EnsureOverlayTarget()
        {
            GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;
            int width = Math.Max(1, Main.screenWidth);
            int height = Math.Max(1, Main.screenHeight);

            if (overlayTarget is not null && !overlayTarget.IsDisposed && overlayTarget.Width == width && overlayTarget.Height == height)
                return;

            overlayTarget?.Dispose();
            overlayTarget = new RenderTarget2D(graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        }

        private static void RenderOverlayTarget()
        {
            GraphicsDevice graphicsDevice = Main.graphics.GraphicsDevice;
            SpriteBatch spriteBatch = Main.spriteBatch;

            spriteBatch.End();
            graphicsDevice.SetRenderTarget(overlayTarget);
            graphicsDevice.Clear(Color.Transparent);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            DrawBlackHoleLensMasks(spriteBatch);
            DrawAntaresFlareMasks(spriteBatch);
            spriteBatch.End();

            graphicsDevice.SetRenderTarget(null);
        }

        private static void DrawOverlayTarget()
        {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Effect shader = GetCompositeShader();

            if (shader is not null)
            {
                shader.Parameters["time"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["screenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
                shader.Parameters["overlayStrength"]?.SetValue(1f);
                shader.Parameters["noiseStrength"]?.SetValue(0.055f);
                shader.Parameters["chromaticStrength"]?.SetValue(0.85f);
                shader.Parameters["noiseTexture"]?.SetValue(AntaresVisualAssetSystem.HaloNoise);
            }

            spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                shader,
                Matrix.Identity);

            spriteBatch.Draw(overlayTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        }

        private static Effect GetCompositeShader()
        {
            if (compositeShader is not null && !compositeShader.IsDisposed)
                return compositeShader;

            try
            {
                byte[] shaderBytes = ModContent.GetInstance<global::TestMod.TestMod>().GetFileBytes(CompositeShaderPath);
                compositeShader = new Effect(Main.graphics.GraphicsDevice, shaderBytes);
            }
            catch (Exception exception)
            {
                if (!loggedShaderLoadFailure)
                {
                    ModContent.GetInstance<global::TestMod.TestMod>().Logger.Warn($"Failed to load shader render target composite shader: {exception.Message}");
                    loggedShaderLoadFailure = true;
                }

                compositeShader = null;
            }

            return compositeShader;
        }

        private static void DrawBlackHoleLensMasks(SpriteBatch spriteBatch)
        {
            Texture2D texture = AntaresVisualAssetSystem.SoftStarMote;
            Vector2 origin = texture.Size() * 0.5f;

            foreach (BlackHoleLensRequest lens in blackHoleLenses)
            {
                Vector2 position = WorldToZoomedScreenPosition(lens.WorldCenter);
                float pulse = 0.88f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5.1f + lens.WorldCenter.X * 0.01f);
                float scale = lens.Radius / Math.Max(texture.Width, 1) * 3.5f * pulse;
                Color color = Color.Lerp(lens.AccretionColor, Color.White, 0.26f) * (0.22f * lens.Opacity);

                spriteBatch.Draw(texture, position, null, color, Main.GlobalTimeWrappedHourly * 0.35f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawAntaresFlareMasks(SpriteBatch spriteBatch)
        {
            Texture2D texture = AntaresVisualAssetSystem.SoftStarMote;
            Vector2 origin = texture.Size() * 0.5f;

            foreach (StarFlareRequest flare in starFlares)
            {
                Vector2 position = WorldToZoomedScreenPosition(flare.WorldCenter);
                float tier = MathHelper.Clamp(flare.Tier / 3f, 0f, 1f);
                float pulse = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * (3.4f + tier) + flare.WorldCenter.Y * 0.01f);
                float scale = flare.Radius / Math.Max(texture.Width, 1) * (2.6f + tier * 0.8f) * pulse;
                Color outer = new Color(255, 74, 20) * (0.16f * flare.Intensity);
                Color inner = new Color(255, 205, 96) * (0.32f * flare.Intensity);

                spriteBatch.Draw(texture, position, null, outer, Main.GlobalTimeWrappedHourly * 0.18f, origin, scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(texture, position, null, inner, -Main.GlobalTimeWrappedHourly * 0.23f, origin, scale * 0.42f, SpriteEffects.None, 0f);
            }
        }

        private static Vector2 WorldToZoomedScreenPosition(Vector2 worldPosition)
        {
            Vector2 screenSize = new(Main.screenWidth, Main.screenHeight);
            Vector2 screenCenter = screenSize * 0.5f;
            Vector2 unzoomed = worldPosition - Main.screenPosition;
            return (unzoomed - screenCenter) * Main.GameViewMatrix.Zoom + screenCenter;
        }

        private readonly struct StarFlareRequest
        {
            public readonly Vector2 WorldCenter;
            public readonly float Radius;
            public readonly float Intensity;
            public readonly int Tier;

            public StarFlareRequest(Vector2 worldCenter, float radius, float intensity, int tier)
            {
                WorldCenter = worldCenter;
                Radius = radius;
                Intensity = intensity;
                Tier = tier;
            }
        }

    }
}
