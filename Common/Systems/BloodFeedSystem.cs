using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Systems
{
    // 暴走期间的屏幕边缘猩红渐晕效果
    // 当前实现：用 SpriteBatch 叠层画矩形，不需要任何外部文件
    // 如需切换为 Luminance 后处理滤镜，将 PostDrawInterface 替换为 Luminance 的
    // ScreenEffectSystem.RegisterFilter 调用，并在 Assets/Effects/Filters/ 提供 .hlsl 文件
    public class BloodFeedSystem : ModSystem
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        private const int   VignetteLayers    = 6;     // 渐晕叠层数（越多越平滑）
        private const int   VignetteMaxThick  = 160;   // 最大渐晕厚度（px）
        private const float PulseSpeed        = 0.18f; // 脉动速度（rad/帧）
        private const float PulseMin          = 0.18f; // 脉动最低亮度系数
        private const float PulseMax          = 0.35f; // 脉动最高亮度系数
        // ─────────────────────────────────────────────────────────────

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            Player player = Main.LocalPlayer;
            if (!player.active) return;

            var mp = player.GetModPlayer<BloodFeedPlayer>();
            if (!mp.IsBerserk) return;

            // 用正弦波驱动亮度脉动，使边框像血液在涌动
            float pulse = PulseMin + (PulseMax - PulseMin)
                        * (float)(Math.Sin(Main.GameUpdateCount * PulseSpeed) * 0.5 + 0.5);

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // 多层叠加：越靠边越不透明，产生渐变效果
            for (int i = 1; i <= VignetteLayers; i++)
            {
                float frac      = (float)i / VignetteLayers;
                int   thick     = (int)(VignetteMaxThick * frac);
                float layerAlpha = pulse * frac * frac * 0.4f; // 二次方让外层更深
                Color c         = new Color(210, 0, 22) * layerAlpha;

                int W = Main.screenWidth, H = Main.screenHeight;
                spriteBatch.Draw(pixel, new Rectangle(0,       0,       W,     thick), c); // 上
                spriteBatch.Draw(pixel, new Rectangle(0,       H-thick, W,     thick), c); // 下
                spriteBatch.Draw(pixel, new Rectangle(0,       0,       thick, H),     c); // 左
                spriteBatch.Draw(pixel, new Rectangle(W-thick, 0,       thick, H),     c); // 右
            }
        }
    }
}
