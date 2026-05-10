using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI.Chat;

namespace TestMod.Common.GlobalItems
{
    // ============================================================================
    //  DamageLineRenderer — 各伤害类型的"伤害"Tooltip行自定义绘制
    // ----------------------------------------------------------------------------
    //  每种职业的专属特效（第三层）各不相同，体现职业气质：
    //    近战           → 锻造火花：伪随机短暂亮点，像击打金属时飞溅的火星
    //    远程           → 子弹曳光：双层扫光（宽暗外晕 + 窄亮弹芯），弹道感
    //    魔法           → 轨道球：三颗光球沿椭圆公转，魔力环绕感
    //    召唤           → 灵魂回音：文字残影缓慢上升并消散，灵体离体感
    //    真实伤害       → 猩红扫光 + 滴血：缓慢血色扫光，血滴从左向右逐字下落
    // ============================================================================
    internal static class DamageLineRenderer
    {
        // ── 数值调节区 ────────────────────────────────────────────────
        // 近战（含 MeleeNoSpeed）
        private static readonly Color MeleeA       = new Color(255,  80,  10); // 橙红
        private static readonly Color MeleeB       = new Color(255, 190,   0); // 金黄
        private const float MeleeGlowSpeed         = 1.8f;
        private const float MeleeSweepSpeed        = 110f;   // px/s 扫光速度
        private const float MeleeSweepWidth        = 28f;
        private const float MeleeSweepInterval     = 2.0f;   // 两次扫光间隔（秒）
        private const int   MeleeSparkCount        = 7;      // 同时存在的火花槽位数
        private const float MeleeSparkMaxPeriod    = 0.55f;  // 每颗火花周期上限（秒）

        // 远程
        private static readonly Color RangedA      = new Color(  0, 210, 160); // 青绿
        private static readonly Color RangedB      = new Color( 80, 240, 215); // 浅青
        private const float RangedGlowSpeed        = 0.7f;
        private const float RangedSweepSpeed       = 260f;   // 极快，象征弹道
        private const float RangedHaloWidth        = 32f;    // 外晕宽度
        private const float RangedCoreWidth        = 10f;    // 弹芯宽度
        private const float RangedSweepInterval    = 1.2f;

        // 魔法
        private static readonly Color MagicA       = new Color(120,  40, 255); // 紫
        private static readonly Color MagicB       = new Color( 40,  90, 255); // 蓝
        private const float MagicGlowSpeed         = 1.4f;
        private const int   MagicOrbitCount        = 3;     // 轨道球数量
        private const float MagicOrbitSpeed        = 2.0f;  // 公转速度（rad/s），约3.1秒一圈
        private const float MagicOrbitRYFixed      = 10f;   // 垂直轨道半径（px，固定值）
        private const float MagicOrbitRXRatio      = 0.55f; // 水平轨道半径 = 文字宽度 × 此值

        // 召唤（含 SummonMeleeSpeed）
        private static readonly Color SummonA      = new Color(  0, 200, 155); // 翠绿
        private static readonly Color SummonB      = new Color(140, 255, 200); // 浅翠
        private const float SummonGlowSpeed        = 0.9f;
        private const int   SummonEchoCount        = 3;     // 同时存在的残影数
        private const float SummonEchoRise         = 16f;   // 残影上升高度（px）
        private const float SummonEchoPeriod       = 1.8f;  // 单条残影完整上升周期（秒）
        private const float SummonEchoMaxAlpha     = 0.38f; // 残影最大透明度

        // 真实伤害（独立实现，不走共用 DrawGlow）
        private static readonly Color TrueGlowA    = new Color(130,   0,  20); // 暗猩红（发光圈低点）
        private static readonly Color TrueGlowB    = new Color(215,  15,  45); // 猩红（发光圈高点）
        private static readonly Color TrueTextBase = new Color(200,  20,  45); // 猩红（主文字）
        private const float TrueGlowSpeed          = 0.5f;   // 发光圈呼吸速度
        private const float TrueSweepSpeed         = 50f;    // 缓慢血色扫光速度（px/s）
        private const float TrueSweepWidth         = 40f;    // 扫光宽度，柔和漫开
        private const float TrueSweepInterval      = 4.0f;   // 两次扫光间隔（秒）
        private const float TrueDripCycle          = 6.0f;   // 滴血完整周期（秒）
        private const float TrueDripSpread         = 3.5f;   // 从第一字到最后一字的蔓延时长（秒）
        private const float TrueDripDuration       = 1.3f;   // 单颗血滴下落持续时长（秒）
        private const float TrueDripMaxDist        = 16f;    // 血滴最大下落距离（px）

        // ── 整体亮度系数 ──────────────────────────────────────────────
        private const float GlowAlpha              = 0.2f;  // 发光圈透明度
        private const float MainBright             = 0.02f; // 主文字阴影亮度（其他职业用）
        private const float SweepBright            = 0.8f;  // 扫光峰值亮度
        // ─────────────────────────────────────────────────────────────

        // ── 公共入口 ──────────────────────────────────────────────────
        public static void DrawMelee(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time)
        {
            var font = FontAssets.MouseText.Value;
            DrawGlow(sb, text, X, Y, rotation, origin, scale, time, font,
                MeleeA, MeleeB, MeleeGlowSpeed, glowRadius: 2.0f, glowLayers: 6);
            DrawSweep(sb, text, X, Y, rotation, origin, scale, time, font,
                new Color(255, 170, 60, 0), MeleeSweepSpeed, MeleeSweepWidth, MeleeSweepInterval);
            DrawForgeSparks(sb, text, X, Y, rotation, origin, scale, time, font);
        }

        public static void DrawRanged(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time)
        {
            var font = FontAssets.MouseText.Value;
            DrawGlow(sb, text, X, Y, rotation, origin, scale, time, font,
                RangedA, RangedB, RangedGlowSpeed, glowRadius: 1.8f, glowLayers: 5);
            DrawSweep(sb, text, X, Y, rotation, origin, scale, time, font,
                new Color(160, 255, 230, 0) * 0.5f, RangedSweepSpeed, RangedHaloWidth, RangedSweepInterval);
            DrawSweep(sb, text, X, Y, rotation, origin, scale, time, font,
                new Color(220, 255, 245, 0), RangedSweepSpeed, RangedCoreWidth, RangedSweepInterval);
        }

        public static void DrawMagic(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time)
        {
            var font = FontAssets.MouseText.Value;
            DrawGlow(sb, text, X, Y, rotation, origin, scale, time, font,
                MagicA, MagicB, MagicGlowSpeed, glowRadius: 2.5f, glowLayers: 8);
            DrawOrbitalDots(sb, text, X, Y, rotation, origin, scale, time, font);
        }

        public static void DrawSummon(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time)
        {
            var font = FontAssets.MouseText.Value;
            DrawGlow(sb, text, X, Y, rotation, origin, scale, time, font,
                SummonA, SummonB, SummonGlowSpeed, glowRadius: 2.8f, glowLayers: 6);
            DrawSoulEchoes(sb, text, X, Y, rotation, origin, scale, time, font);
        }

        // 真实伤害独立实现：
        //   1. 暗猩红发光圈呼吸
        //   2. 稳定猩红主文字（纯阴影层，不加发光叠加）
        //   3. 缓慢血色扫光
        //   4. 逐字滴血（从第一字到最后一字依次下落）
        public static void DrawTrue(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time)
        {
            var font = FontAssets.MouseText.Value;
            Vector2 pos = new Vector2(X, Y);

            // ── 1. 发光圈：暗猩红呼吸 ────────────────────────────────
            float glowT   = (float)(Math.Sin(time * TrueGlowSpeed) * 0.5 + 0.5);
            Color glowClr = Color.Lerp(TrueGlowA, TrueGlowB, glowT);
            glowClr.A = 0;
            float pulsedR = 2.0f + 0.35f * (float)Math.Sin(time * 2.8f);
            for (int i = 0; i < 6; i++)
            {
                float   angle  = MathHelper.TwoPi / 6f * i + time * 0.9f;
                Vector2 offset = new Vector2(
                    (float)Math.Cos(angle) * pulsedR,
                    (float)Math.Sin(angle) * pulsedR * 0.45f);
                ChatManager.DrawColorCodedString(sb, font, text, pos + offset,
                    glowClr * GlowAlpha, rotation, origin, scale);
            }

            // ── 2. 主文字：稳定猩红，仅阴影层 ───────────────────────
            ChatManager.DrawColorCodedStringShadow(sb, font, text, pos,
                TrueTextBase, rotation, origin, scale);

            // ── 3. 缓慢血色扫光 ───────────────────────────────────────
            DrawSweep(sb, text, X, Y, rotation, origin, scale, time, font,
                new Color(200, 10, 30, 0), TrueSweepSpeed, TrueSweepWidth, TrueSweepInterval);

            // ── 4. 逐字滴血 ───────────────────────────────────────────
            DrawBloodDrip(sb, text, X, Y, rotation, origin, scale, time, font);
        }

        // ═══════════════════════════════════════════════════════════════
        //  底层绘制原语
        // ═══════════════════════════════════════════════════════════════

        // ── 发光层 + 主文字（近战/远程/魔法/召唤共用）─────────────────
        private static void DrawGlow(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time, DynamicSpriteFont font,
            Color colorA, Color colorB, float glowSpeed, float glowRadius, int glowLayers)
        {
            Vector2 pos = new Vector2(X, Y);
            float t     = (float)(Math.Sin(time * glowSpeed) * 0.5 + 0.5);
            Color main  = Color.Lerp(colorA, colorB, t);
            main.A = 0;

            float pulsedR = glowRadius + 0.35f * (float)Math.Sin(time * 2.8f);
            for (int i = 0; i < glowLayers; i++)
            {
                float angle    = MathHelper.TwoPi / glowLayers * i + time * 0.9f;
                Vector2 offset = new Vector2(
                    (float)Math.Cos(angle) * pulsedR,
                    (float)Math.Sin(angle) * pulsedR * 0.45f);
                ChatManager.DrawColorCodedString(sb, font, text, pos + offset,
                    main * GlowAlpha, rotation, origin, scale);
            }

            Color solid = main;
            solid.A = 255;
            ChatManager.DrawColorCodedStringShadow(sb, font, text, pos,
                solid * MainBright, rotation, origin, scale);
            main.A = 0;
            ChatManager.DrawColorCodedString(sb, font, text, pos, main, rotation, origin, scale);
        }

        // ── 扫光（逐字符，支持任意速度/宽度/颜色）────────────────────
        private static void DrawSweep(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time, DynamicSpriteFont font,
            Color sweepColor, float speed, float width, float interval)
        {
            float textW     = font.MeasureString(text).X * scale.X;
            float cycle     = textW + width * 2f + Math.Abs(speed) * interval;
            float shinePosX = (time * speed % cycle + cycle) % cycle - width;

            float charOffX = 0f;
            foreach (char c in text)
            {
                string cs       = c.ToString();
                float  cW       = font.MeasureString(cs).X * scale.X;
                float  cCenter  = charOffX + cW * 0.5f;
                float  dist     = Math.Abs(cCenter - shinePosX);
                float  intensity = 1f - MathHelper.Clamp(dist / width, 0f, 1f);
                intensity *= intensity;
                if (intensity > 0.01f)
                    ChatManager.DrawColorCodedString(sb, font, cs,
                        new Vector2(X + charOffX, Y),
                        sweepColor * intensity * SweepBright, rotation, origin, scale);
                charOffX += cW;
            }
        }

        // ── 近战专属：锻造火花 ────────────────────────────────────────
        private static void DrawForgeSparks(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time, DynamicSpriteFont font)
        {
            Vector2 textSize = font.MeasureString(text) * scale;

            for (int i = 0; i < MeleeSparkCount; i++)
            {
                float period = MeleeSparkMaxPeriod * (0.6f + (float)Math.Abs(Math.Sin(i * 131.7f)) * 0.4f);
                float phase  = (float)(Math.Sin(i * 74.3f) * MathHelper.TwoPi);
                float t      = ((time + phase) % period + period) % period / period;

                if (t > 0.30f) continue;
                float brightness = 1f - t / 0.30f;
                brightness *= brightness * brightness;

                float bucket = MathF.Floor((time + phase) / period);
                float sx = MathF.Abs(MathF.Sin(bucket * 127.1f + i * 31.7f));
                float sy = MathF.Abs(MathF.Sin(bucket * 311.7f + i * 74.3f));
                Vector2 sparkPos = new Vector2(X + sx * textSize.X, Y + sy * textSize.Y);

                Color clr = Color.Lerp(new Color(255, 180, 60), Color.White, brightness);
                clr.A = 0;
                ChatManager.DrawColorCodedString(sb, font, "·", sparkPos,
                    clr * brightness * 0.9f, rotation, origin, scale * (0.5f + brightness * 0.6f));
            }
        }

        // ── 魔法专属：轨道球 ──────────────────────────────────────────
        private static void DrawOrbitalDots(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time, DynamicSpriteFont font)
        {
            Vector2 textSize = font.MeasureString(text) * scale;
            Vector2 center   = new Vector2(X + textSize.X * 0.5f, Y + textSize.Y * 0.5f);
            float   orbitRX  = textSize.X * MagicOrbitRXRatio;

            for (int i = 0; i < MagicOrbitCount; i++)
            {
                float phase = (float)i / MagicOrbitCount * MathHelper.TwoPi;
                float angle = time * MagicOrbitSpeed + phase;

                Vector2 dotPos = center + new Vector2(
                    (float)Math.Cos(angle) * orbitRX,
                    (float)Math.Sin(angle) * MagicOrbitRYFixed);

                float brightness = (float)(Math.Sin(time * 3f + phase) * 0.25 + 0.75);
                Color dotClr = Color.Lerp(MagicA, MagicB,
                    (float)(Math.Sin(time * 1.5f + phase) * 0.5 + 0.5));
                dotClr.A = 0;

                ChatManager.DrawColorCodedString(sb, font, "·", dotPos,
                    dotClr * brightness * 0.9f, rotation, origin, scale * (0.8f + brightness * 0.3f));
            }
        }

        // ── 召唤专属：灵魂回音 ────────────────────────────────────────
        private static void DrawSoulEchoes(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time, DynamicSpriteFont font)
        {
            for (int i = 0; i < SummonEchoCount; i++)
            {
                float phase = i * (1f / SummonEchoCount);
                float t     = ((time / SummonEchoPeriod + phase) % 1f + 1f) % 1f;
                float riseY = t * SummonEchoRise;
                float alpha = (1f - t) * SummonEchoMaxAlpha;
                if (alpha < 0.01f) continue;

                Color echoClr = Color.Lerp(SummonA, SummonB, t);
                echoClr.A = 0;
                ChatManager.DrawColorCodedString(sb, font, text,
                    new Vector2(X, Y - riseY), echoClr * alpha, rotation, origin, scale);
            }
        }

        // ── 真实伤害专属：逐字滴血 ───────────────────────────────────
        // 整颗血滴（🩸 轮廓：尖顶圆底）作为整体从字符底部出现并下落，
        // 不再是单粒子，而是由 DropProfile 中多层 · 堆叠逼近泪滴形状。
        private static void DrawBloodDrip(SpriteBatch sb, string text, int X, int Y,
            float rotation, Vector2 origin, Vector2 scale, float time, DynamicSpriteFont font)
        {
            if (text.Length == 0) return;

            float cycleTime = time % TrueDripCycle;
            float charH     = font.MeasureString("A").Y * scale.Y;
            int   n         = text.Length;
            float charOffX  = 0f;

            for (int i = 0; i < n; i++)
            {
                string cs        = text[i].ToString();
                float  cW        = font.MeasureString(cs).X * scale.X;
                float  dripStart = (float)i / n * TrueDripSpread;
                float  localT    = cycleTime - dripStart;

                if (localT >= 0f && localT < TrueDripDuration)
                {
                    float progress = localT / TrueDripDuration;

                    float alpha;
                    if      (progress < 0.12f) alpha = progress / 0.12f;
                    else if (progress < 0.70f) alpha = 1.0f;
                    else                        alpha = 1f - (progress - 0.70f) / 0.30f;
                    alpha = MathHelper.Clamp(alpha, 0f, 1f);

                    // 血滴尖端（顶部）的位置：字符底部 + 随进度下落
                    float cx   = X + charOffX + cW * 0.5f;
                    float tipY = Y + charH * 0.88f + progress * TrueDripMaxDist;

                    DrawBloodDropShape(sb, font, cx, tipY, alpha, scale, rotation, origin);
                }

                charOffX += cW;
            }
        }

        // 🩸 血滴轮廓：(y偏移px, 点的缩放系数, 亮度系数)
        //   y=0 为尖顶，向下依次加宽，在约 7.5px 处达到最宽，之后收窄为圆底
        //   数值经手工调整以逼近泪滴/血滴形状，可自由调整
        private static readonly (float dy, float s, float bright)[] DropProfile =
        {
            ( 0.0f, 0.18f, 0.60f),  // 尖端（极细）
            ( 2.0f, 0.40f, 0.75f),  // 上颈
            ( 4.0f, 0.70f, 0.90f),  // 上身
            ( 6.5f, 0.98f, 1.00f),  // 最宽处
            ( 9.0f, 1.00f, 0.98f),  // 下腹（略宽，圆弧开始）
            (11.5f, 0.82f, 0.88f),  // 下圆弧
            (13.5f, 0.55f, 0.70f),  // 底部圆弧收尾
        };

        // 按 DropProfile 在 (cx, tipY) 处绘制完整血滴形状
        private static void DrawBloodDropShape(SpriteBatch sb, DynamicSpriteFont font,
            float cx, float tipY, float alpha, Vector2 scale, float rotation, Vector2 origin)
        {
            float baseDotW = font.MeasureString("·").X;

            foreach (var (dy, s, bright) in DropProfile)
            {
                float dotW  = baseDotW * s * scale.X;
                float drawX = cx - dotW * 0.5f;                // 水平居中
                float drawY = tipY + dy * scale.Y;

                Color clr = new Color(182, 4, 16) * (alpha * bright);
                ChatManager.DrawColorCodedString(sb, font, "·",
                    new Vector2(drawX, drawY), clr, rotation, origin, scale * s);
            }
        }
    }
}
