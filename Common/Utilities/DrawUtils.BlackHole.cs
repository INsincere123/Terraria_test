using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using TestMod.Common.Systems;

namespace TestMod.Common.Utilities
{
    public static partial class DrawUtils
    {
        public const string BlackHoleShaderName = "TestMod.BlackHoleShader";

        /// <summary>
        /// Draws a shader-generated black hole and registers its screen-space lensing for this frame.
        /// </summary>
        public static bool DrawBlackHole(
            SpriteBatch spriteBatch,
            Vector2 worldCenter,
            float radius,
            Color accretionDiskColor,
            float opacity = 1f,
            bool registerLens = true)
        {
            if (Main.dedServ || radius <= 0f || opacity <= 0f)
                return false;

            if (registerLens)
                GravitationalLensSystem.RegisterBlackHole(worldCenter, radius, opacity, accretionDiskColor);

            if (registerLens)
                return true;

            Vector2 screenPosition = worldCenter - Main.screenPosition;
            float diameter = radius * 2.8f;

            if (!ShaderManager.TryGetShader(BlackHoleShaderName, out ManagedShader shader))
                return true;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Texture2D noise = AntaresVisualAssetSystem.HaloNoise;

            shader.TrySetParameter("time", Main.GlobalTimeWrappedHourly);
            shader.TrySetParameter("blackRadius", 0.24f);
            shader.TrySetParameter("opacity", opacity);
            shader.TrySetParameter("accretionDiskColor", accretionDiskColor.ToVector3());
            shader.SetTexture(noise, 1, SamplerState.LinearWrap);

            spriteBatch.RestartSpriteBatch(
                SpriteSortMode.Immediate,
                SpriteBatchSettings.AlphaBlendLinearClamp,
                matrix: Main.GameViewMatrix.TransformationMatrix);

            shader.Apply();
            spriteBatch.Draw(
                pixel,
                screenPosition,
                new Rectangle(0, 0, 1, 1),
                Color.White,
                0f,
                new Vector2(0.5f),
                new Vector2(diameter),
                SpriteEffects.None,
                0f);

            spriteBatch.ExitShaderRegion();
            return true;
        }
    }
}
