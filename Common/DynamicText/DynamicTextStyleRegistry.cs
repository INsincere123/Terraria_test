using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Common.DynamicText.Fonts;
using TestMod.Items.DamageTypes;

namespace TestMod.Common.DynamicText
{
    public static class DynamicTextStyleRegistry
    {
        public const string DamageMelee = "Damage.Melee";
        public const string DamageRanged = "Damage.Ranged";
        public const string DamageMagic = "Damage.Magic";
        public const string DamageSummon = "Damage.Summon";
        public const string DamageTrue = "Damage.True";
        public const string ExtraHit = "Combat.ExtraHit";
        public const string BlackHoleAbsorb = "Combat.BlackHoleAbsorb";
        public const string Subhand = "Combat.Subhand";
        public const string Supercrit = "Combat.Supercrit";
        public const string Antares = "Combat.Antares";
        public const string BloodFeedBerserk = "Combat.BloodFeedBerserk";
        public const string Heartsteel = "Combat.Heartsteel";
        public const string EnergyShieldDamage = "EnergyShield.Damage";
        public const string EnergyShieldBreak = "EnergyShield.Break";
        public const string RarityAntares = "Rarity.Antares";
        public const string RarityEventHorizon = "Rarity.EventHorizon";

        private static readonly Dictionary<string, DynamicTextStyle> Styles = new();

        public static IEnumerable<DynamicTextStyle> RegisteredStyles => Styles.Values;

        static DynamicTextStyleRegistry()
        {
            RegisterDefaults();
        }

        public static DynamicTextStyle Get(string key)
        {
            if (key is not null && Styles.TryGetValue(key, out DynamicTextStyle style))
                return style;

            return Styles[ExtraHit];
        }

        public static void Register(DynamicTextStyle style)
        {
            Styles[style.Key] = style;
        }

        public static bool TryGetTooltipDamageStyle(DamageClass damageClass, out DynamicTextStyle style)
        {
            string key = null;

            if (damageClass == TrueDamageClass.Instance)
                key = DamageTrue;
            else if (damageClass == DamageClass.Melee || damageClass == DamageClass.MeleeNoSpeed)
                key = DamageMelee;
            else if (damageClass == DamageClass.Ranged)
                key = DamageRanged;
            else if (damageClass == DamageClass.Magic)
                key = DamageMagic;
            else if (damageClass == DamageClass.Summon || damageClass == DamageClass.SummonMeleeSpeed)
                key = DamageSummon;

            if (key is not null)
            {
                style = Get(key);
                return true;
            }

            style = null;
            return false;
        }

        private static void RegisterDefaults()
        {
            Register(new DynamicTextStyle(DamageMelee)
            {
                PrimaryColor = new Color(255, 24, 34),
                SecondaryColor = new Color(92, 0, 8),
                ShadowColor = Color.Black,
                Layers =
                [
                    new TextRuptureLayer(new Color(255, 58, 62), 8, 15f, 1.15f, 0.72f),
                    new TextChromaticLayer(0.7f, 0.18f, 11f),
                    new TextGlowLayer(5, 1.9f, 0.2f, 4.4f, 0.32f),
                    new TextOutlineLayer(2.1f, 1f),
                ]
            });

            Register(new DynamicTextStyle(DamageRanged)
            {
                PrimaryColor = new Color(58, 238, 210),
                SecondaryColor = new Color(210, 255, 236),
                ShadowColor = Color.Black,
                Layers =
                [
                    new TextReticleLayer(new Color(160, 255, 226), 7.5f, 4.2f, 1.15f, 0.62f),
                    new TextTracerLayer(new Color(72, 255, 216), 5, 390f, 30f, 1.05f, 0.44f),
                    new TextChromaticLayer(0.72f, 0.16f, 7.5f),
                    new TextGlowLayer(4, 1.8f, 0.22f, 2.8f, 0.42f),
                    new TextOutlineLayer(1.45f, 0.82f)
                ]
            });

            Register(new DynamicTextStyle(DamageMagic)
            {
                PrimaryColor = new Color(124, 74, 255),
                SecondaryColor = new Color(80, 238, 255),
                ShadowColor = Color.Black,
                Layers =
                [
                    new TextArcaneOrbitLayer(new Color(210, 158, 255), 7, 7.5f, 1.18f, 2.6f, 0.68f),
                    new TextManaWispLayer(new Color(126, 255, 242), 8, 18f, 0.74f, 1.35f, 0.42f),
                    new TextGlowLayer(11, 3.4f, 0.34f, 1.9f, 0.85f),
                    new TextEchoLayer(3, new Vector2(0f, -15f), 1.35f, 0.22f),
                    new TextGlyphWaveLayer(new Color(116, 244, 255), new Vector2(0.28f, -1f), 3.1f, 0.94f, 5.8f, 0.5f),
                    new TextSparkleLayer(10, 4.8f, 0.58f),
                    new TextOutlineLayer(1.45f, 0.72f)
                ]
            });

            Register(new DynamicTextStyle(DamageSummon)
            {
                PrimaryColor = new Color(82, 232, 154),
                SecondaryColor = new Color(178, 255, 214),
                ShadowColor = Color.Black,
                Layers =
                [
                    new TextSoulWispLayer(new Color(205, 255, 224), 7, 8f, 1.05f, 2.2f, 0.62f),
                    new TextEchoLayer(4, new Vector2(0f, -22f), 1.9f, 0.32f),
                    new TextGlowLayer(7, 3.1f, 0.28f, 1.35f, 0.7f),
                    new TextGlyphWaveLayer(new Color(166, 255, 204), new Vector2(0f, -1f), 2.2f, 1.1f, 3.8f, 0.32f),
                    new TextOutlineLayer(1.55f, 0.72f)
                ]
            });

            Register(new DynamicTextStyle(DamageTrue)
            {
                FontSpec = DynamicTextFontSpec.Create(
                    27f, // 字体大小
                    //DynamicTextFontCandidate.ByName("宋体"),
                    DynamicTextFontCandidate.ByName("Harrington") // 字体名称，指定用于显示的字体
                    //DynamicTextFontCandidate.ByPath(@"C:\Windows\Fonts\bahnschrift.ttc", "bahnschrift") // 字体路径，指定用于显示的字体文件
                    ),
                PrimaryColor = Color.White, // 主要颜色，文本的主色调
                SecondaryColor = new Color(210, 245, 255), // 次要颜色，用于渐变或高光效果
                Velocity = new Vector2(0f, -2.35f), // 初始速度，文本移动方向与速度
                RandomVelocityMin = new Vector2(-2.3f, -6.5f),
                RandomVelocityMax = new Vector2(2.3f, 0f),
                Lifetime = 64, // 存活时间，文本显示的帧数
                Gravity = 0.014f, // 重力影响，控制文本垂直加速度
                Drag = 0.972f, // 阻力系数，控制速度衰减
                BaseScale = 1.0f, // 基础缩放，文本默认大小比例
                CritScaleBonus = 0.22f, // 暴击时额外缩放倍率加成
                ShadowColor = new Color(0, 0, 0), // 阴影颜色，用于文本投影
                Layers = // 文本效果层列表，按顺序渲染附加特效
                [
                    new TextGlowLayer(6, 2.8f, 0.38f, 1.6f, 0.62f), // 发光层: 强度, 半径, 透明度, 速度, 亮度
                    new TextManaWispLayer(new Color(235, 255, 255), 6, 13f, 0.52f, 1.1f, 0.24f), // 法力光尘层: 颜色, 数量, 半径, 速度, 缩放, 透明度
                    new TextSweepLayer(Color.White, 185f, 34f, 1.05f, 0.84f), // 扫光层: 颜色, 角度, 速度, 缩放, 透明度
                    new TextSparkleLayer(6, 4.6f, 0.5f), // 火花层: 数量, 半径, 透明度
                    new TextOutlineLayer(1.65f, 0.78f), // 描边层: 描边宽度, 透明度
                ]
            });

            Register(new DynamicTextStyle(ExtraHit)
            {
                PrimaryColor = new Color(180, 50, 255),
                SecondaryColor = new Color(255, 104, 236),
                ShadowColor = Color.Black,
                Velocity = new Vector2(0f, -2.35f),
                RandomVelocityMin = new Vector2(-2f, -2.88f),
                RandomVelocityMax = new Vector2(2f, 0f),
                Lifetime = 66,
                Gravity = 0.02f,
                Drag = 0.975f,
                BaseScale = 1.05f,
                CritScaleBonus = 0.26f,
                Layers =
                [
                    new TextChromaticLayer(0.85f, 0.22f, 4.6f),
                    new TextGlowLayer(8, 2.8f, 0.34f, 2.4f),
                    new TextEchoLayer(2, new Vector2(0f, 12f), 0.9f, 0.22f),
                    new TextTracerLayer(new Color(236, 186, 255), 3, 235f, 22f, 1.2f, 0.28f),
                    new TextSweepLayer(Color.White, 215f, 30f, 0.72f, 0.72f),
                    new TextOutlineLayer(1.85f, 0.95f)
                ]
            });

            Register(new DynamicTextStyle(Subhand)
            {
                PrimaryColor = new Color(150, 255, 188),
                SecondaryColor = new Color(66, 255, 198),
                ShadowColor = Color.Black,
                Velocity = new Vector2(0.55f, -1.65f),
                Lifetime = 62,
                Gravity = 0.018f,
                Drag = 0.972f,
                BaseScale = 0.96f,
                CritScaleBonus = 0.18f,
                Layers =
                [
                    new TextGlowLayer(6, 2.3f, 0.28f, 2.2f),
                    new TextSweepLayer(new Color(222, 255, 232), 205f, 24f, 0.78f, 0.62f),
                    new TextOutlineLayer(1.75f, 0.95f)
                ]
            });

            Register(new DynamicTextStyle(BlackHoleAbsorb)
            {
                PrimaryColor = new Color(218, 148, 40),
                SecondaryColor = new Color(96, 48, 180),
                Velocity = new Vector2(0f, -1.35f),
                Lifetime = 64,
                Gravity = -0.006f,
                Drag = 0.96f,
                BaseScale = 0.82f,
                EndScale = 0.55f,
                Layers =
                [
                    new TextGlowLayer(8, 3.1f, 0.4f, 1.8f),
                    new TextEchoLayer(2, new Vector2(0f, 10f), 0.95f, 0.2f),
                    new TextSweepLayer(new Color(255, 200, 82), 70f, 30f, 1.1f, 0.65f),
                    new TextShadowLayer(1.25f)
                ]
            });

            Register(new DynamicTextStyle(Supercrit)
            {
                PrimaryColor = new Color(255, 214, 62),
                SecondaryColor = new Color(255, 72, 172),
                Velocity = new Vector2(0f, 1.45f),
                RandomVelocityMin = new Vector2(-2f, 0f),
                RandomVelocityMax = new Vector2(2f, 3.88f),
                Lifetime = 44,
                Gravity = -0.012f,
                Drag = 0.965f,
                BaseScale = 0.64f,
                EndScale = 0.72f,
                CritScaleBonus = 0.08f,
                Layers =
                [
                    new TextGlowLayer(5, 1.9f, 0.32f, 3.2f),
                    new TextSparkleLayer(3, 4.2f, 0.5f),
                    new TextSweepLayer(Color.White, 180f, 18f, 0.6f, 0.72f),
                    new TextShadowLayer(1.18f)
                ]
            });

            Register(new DynamicTextStyle(Antares)
            {
                PrimaryColor = new Color(88, 160, 255),
                SecondaryColor = new Color(255, 112, 48),
                Velocity = new Vector2(0f, -1.7f),
                Lifetime = 86,
                Gravity = 0.008f,
                BaseScale = 1.05f,
                Layers =
                [
                    new TextGlowLayer(9, 3f, 0.38f, 1.5f),
                    new TextSparkleLayer(10, 6.5f, 0.72f),
                    new TextEchoLayer(2, new Vector2(0f, -13f), 1.4f, 0.24f),
                    new TextShadowLayer(1.28f)
                ]
            });

            Register(new DynamicTextStyle(BloodFeedBerserk)
            {
                PrimaryColor = new Color(220, 18, 48),
                SecondaryColor = new Color(80, 0, 18),
                Velocity = new Vector2(0f, -2.2f),
                Lifetime = 82,
                Gravity = 0.01f,
                BaseScale = 1.08f,
                CritScaleBonus = 0.35f,
                Layers =
                [
                    new TextGlowLayer(9, 3.4f, 0.42f, 2.2f),
                    new TextSweepLayer(new Color(255, 82, 92), 130f, 42f, 0.85f, 0.82f),
                    new TextBloodDripLayer(new Color(180, 4, 14), 3.6f, 1.2f, 1f, 18f, 0.95f),
                    new TextEchoLayer(2, new Vector2(0f, -18f), 1.1f, 0.24f),
                    new TextShadowLayer(1.42f)
                ]
            });

            Register(new DynamicTextStyle(Heartsteel)
            {
                PrimaryColor = new Color(190, 70, 255),
                SecondaryColor = new Color(255, 186, 72),
                ShadowColor = Color.Black,
                Velocity = new Vector2(0f, -2f),
                Lifetime = 78,
                Gravity = 0.015f,
                BaseScale = 1.05f,
                CritScaleBonus = 0.38f,
                Layers =
                [
                    new TextGlowLayer(8, 3.1f, 0.42f, 1.6f),
                    new TextSweepLayer(new Color(255, 215, 110), 150f, 38f, 0.9f, 0.82f),
                    new TextSparkleLayer(7, 6.5f, 0.75f),
                    new TextOutlineLayer(1.85f, 0.95f)
                ]
            });

            Register(new DynamicTextStyle(EnergyShieldDamage)
            {
                FontSpec = DynamicTextFontSpec.Create(
                    24f,
                    DynamicTextFontCandidate.ByName("Agency FB")
                ),
                PrimaryColor = new Color(64, 224, 255),
                SecondaryColor = new Color(64, 224, 255),
                Velocity = new Vector2(0f, -1.55f),
                RandomVelocityMin = new Vector2(-1.5f, -0.5f),
                RandomVelocityMax = new Vector2(1.5f, 0.5f),
                Lifetime = 54,
                Gravity = 0.01f,
                Drag = 0.975f,
                BaseScale = 0.9f,
                EndScale = 0.78f,
                CritScaleBonus = 0f,
                Layers =
                [
                    new TextPlainLayer()
                ]
            });

            Register(new DynamicTextStyle(EnergyShieldBreak)
            {
                FontSpec = DynamicTextFontSpec.Create(
                    26f,
                    DynamicTextFontCandidate.ByName("黑体")
                ),
                PrimaryColor = new Color(64, 224, 255),
                SecondaryColor = new Color(64, 224, 255),
                Velocity = new Vector2(0f, -1.25f),
                Lifetime = 72,
                Gravity = 0.006f,
                Drag = 0.98f,
                BaseScale = 1.0f,
                EndScale = 0.86f,
                CritScaleBonus = 0f,
                Layers =
                [
                    new TextPlainLayer()
                ]
            });

            Register(new DynamicTextStyle(RarityAntares)
            {
                PrimaryColor = new Color(30, 60, 180),
                SecondaryColor = new Color(255, 112, 52),
                Layers =
                [
                    new TextGlowLayer(8, 2.3f, 0.34f, 1.1f),
                    new TextSweepLayer(new Color(220, 236, 255), 90f, 35f, 3.5f, 0.72f),
                    new TextSparkleLayer(5, 5f, 0.72f),
                    new TextShadowLayer(1.5f)
                ]
            });

            Register(new DynamicTextStyle(RarityEventHorizon)
            {
                PrimaryColor = new Color(92, 30, 118),
                SecondaryColor = new Color(255, 166, 55),
                Layers =
                [
                    new TextGlowLayer(10, 2.25f, 0.34f, 1.3f),
                    new TextSweepLayer(new Color(255, 118, 42), 56f, 44f, 2.8f, 0.55f),
                    new TextSparkleLayer(4, 4.6f, 0.46f),
                    new TextShadowLayer(1.52f)
                ]
            });
        }
    }
}
