using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace TestMod.Rarities
{
    public class AntaresRarity : ModRarity
    {
        // ╔══════════════════════════════════════════════════════╗
        // ║              深空星海稀有度参数调整区域              ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【主色调】                                          ║
        // ║    ColorA        渐变起始色（深蓝）                  ║
        // ║    ColorB        渐变终止色（深紫）                  ║
        // ║    GlowSpeed     颜色渐变周期速度（越大越快）        ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【光晕层】                                          ║
        // ║    GlowRadius    文字外发光半径（像素）              ║
        // ║    GlowLayers    发光叠加层数（越多越亮，性能越低）  ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【星光扫描】                                        ║
        // ║    ShineSpeed    扫光移动速度（像素/秒）             ║
        // ║    ShineWidth    扫光宽度（像素）                    ║
        // ║    ShineInterval 两次扫光之间的间隔（秒）            ║
        // ╠══════════════════════════════════════════════════════╣
        // ║  【闪烁星点】                                        ║
        // ║    SparkleCount  同时存在的星点数量                  ║
        // ║    SparkleSpeed  星点闪烁速度                        ║
        // ╚══════════════════════════════════════════════════════╝

        // 主色调
        public static readonly Color ColorA      = new Color(30,  60,  180); // 深蓝
        public static readonly Color ColorB      = new Color(90,  20,  160); // 深紫
        public const float           GlowSpeed   = 0.8f;

        // 光晕层
        public const float GlowRadius = 2.2f;
        public const int   GlowLayers = 8;

        // 扫光
        public const float ShineSpeed    = 90f;
        public const float ShineWidth    = 35f;
        public const float ShineInterval = 3.5f;

        // 星点
        public const int   SparkleCount = 5;
        public const float SparkleSpeed = 4f;

        // ── 物品栏图标颜色（静态，取两色中间值）────────────────
        public override Color RarityColor => Color.Lerp(ColorA, ColorB, 0.5f) * 2f;

        // ── 主绘制入口（由 TestGlobalItem.PreDrawTooltipLine 调用）
        public static void Draw(Item item, DrawableTooltipLine line)
        {
            Draw(item, Main.spriteBatch, line.Text, line.X, line.Y,
                line.Rotation, line.Origin, line.BaseScale,
                Main.GlobalTimeWrappedHourly, FontAssets.MouseText.Value);
        }

        public static void Draw(Item item, SpriteBatch sb, string text,
            int X, int Y, float rotation, Vector2 origin, Vector2 baseScale,
            float time, DynamicSpriteFont font)
        {
            Vector2 pos      = new Vector2(X, Y);
            Vector2 fontSize = font.MeasureString(text);

            // ── 1. 计算当前主色（深蓝↔深紫缓慢渐变）────────────
            float t       = (float)(Math.Sin(time * GlowSpeed) * 0.5 + 0.5);
            Color mainClr = Color.Lerp(ColorA, ColorB, t);
            mainClr.A = 0; // 加法混合去掉 alpha 遮罩

            // ── 2. 外发光层（多圈偏移叠加）──────────────────────
            float glowPulse = GlowRadius + 0.5f * (float)Math.Sin(time * 3f);
            for (int i = 0; i < GlowLayers; i++)
            {
                float angle    = MathHelper.TwoPi / GlowLayers * i + time * 1.2f;
                Vector2 offset = new Vector2(
                    (float)Math.Cos(angle) * glowPulse,
                    (float)Math.Sin(angle) * glowPulse * 0.5f);
                Color glowClr = mainClr * 0.28f;
                ChatManager.DrawColorCodedString(sb, font, text, pos + offset,
                    glowClr, rotation, origin, baseScale);
            }

            // ── 3. 主文字（带阴影）──────────────────────────────
            Color solidClr = mainClr;
            solidClr.A = 255;
            ChatManager.DrawColorCodedStringShadow(sb, font, text, pos,
                solidClr * 1.5f, rotation, origin, baseScale);

            mainClr.A = 0;
            ChatManager.DrawColorCodedString(sb, font, text, pos,
                mainClr, rotation, origin, baseScale);

            // ── 4. 扫光（周期性白色高光从左扫到右）─────────────
            // 用 time 对 (文字宽 + 间隔) 取模，控制扫光出现频率
            float cycle    = fontSize.X + ShineSpeed * ShineInterval;
            float shinePosX = (time * ShineSpeed) % cycle - ShineWidth;

            // 逐字符判断是否在扫光范围内
            float charOffsetX = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                string c        = text[i].ToString();
                Vector2 cSize   = font.MeasureString(c);
                float   centerX = X + charOffsetX + cSize.X * baseScale.X * 0.5f;
                float   dist    = Math.Abs(centerX - (X + shinePosX));
                float   intensity = 1f - MathHelper.Clamp(dist / ShineWidth, 0f, 1f);

                if (intensity > 0f)
                {
                    // 扫光颜色：冷白带淡蓝
                    Color shineClr = new Color(200, 220, 255, 0) * intensity * 1.6f;
                    ChatManager.DrawColorCodedString(sb, font, c,
                        pos + new Vector2(charOffsetX, 0f),
                        shineClr, rotation, origin, baseScale);
                }

                charOffsetX += cSize.X - text.Length * 0.0085f;
            }

            // ── 5. 闪烁星点（固定伪随机位置，亮度随时间正弦闪烁）
            for (int i = 0; i < SparkleCount; i++)
            {
                // 用索引生成固定的伪随机偏移，让星点位置不乱跳
                float seedX   = (float)Math.Sin(i * 127.1f) * 0.5f + 0.5f;
                float seedY   = (float)Math.Sin(i * 311.7f) * 0.5f + 0.5f;
                float phase   = (float)Math.Sin(i * 74.3f) * MathHelper.TwoPi;

                Vector2 sparklePos = new Vector2(
                    X + seedX * fontSize.X,
                    Y + seedY * fontSize.Y);

                float brightness = (float)(Math.Sin(time * SparkleSpeed + phase) * 0.5 + 0.5);
                brightness = (float)Math.Pow(brightness, 3f); // 集中在高亮时刻

                if (brightness > 0.05f)
                {
                    // 十字星形：4条短线
                    Color sparkClr = new Color(220, 230, 255, 0) * brightness;
                    float size     = 3f * brightness;
                    for (int d = 0; d < 4; d++)
                    {
                        float ang = MathHelper.PiOver2 * d;
                        Vector2 sparkOffset = new Vector2(
                            (float)Math.Cos(ang) * size,
                            (float)Math.Sin(ang) * size);
                        ChatManager.DrawColorCodedString(sb, font, "·",
                            sparklePos + sparkOffset, sparkClr,
                            rotation, origin, baseScale * (0.5f + brightness * 0.5f));
                    }
                }
            }
        }
    }
}
