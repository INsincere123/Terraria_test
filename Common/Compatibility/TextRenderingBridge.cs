using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.DamageTypes;

namespace TestMod.Common.Compatibility
{
    // 可选渲染增强。此处没有纹理、布局缓存、SpriteBatch 或新模组类型引用。
    public static class TextRenderingBridge
    {
        private static Mod renderer;
        private static bool loggedFailure;

        internal static void Load()
        {
            loggedFailure = false;
            renderer = null;
            if (!Main.dedServ)
                ModLoader.TryGetMod("textranderingsystem", out renderer);
        }

        internal static void Unload()
        {
            renderer = null;
            loggedFailure = false;
        }

        private static object Call(params object[] args)
        {
            if (renderer is null || Main.dedServ)
                return null;
            try
            {
                return renderer.Call(args);
            }
            catch (Exception exception)
            {
                if (!loggedFailure)
                {
                    loggedFailure = true;
                    ModContent.GetInstance<global::TestMod.TestMod>().Logger.Warn(
                        $"Optional text rendering call '{args[0]}' failed; using vanilla text: {exception}");
                }
                // 避免每帧重复调用失效接口；下一次模组重载重新解析。
                renderer = null;
                return null;
            }
        }

        public static Color GetStyleColor(string styleKey) =>
            Call("GetStyleColor", styleKey) is Color color ? color : TestModTextStyles.GetFallbackColor(styleKey);

        public static void Spawn(DynamicWorldTextRequest request)
        {
            if (Main.netMode == NetmodeID.Server || string.IsNullOrWhiteSpace(request.Text))
                return;
            if (Call("SpawnWorldText", request.Text, request.WorldPosition, request.StyleKey,
                request.OverrideColor, request.Crit, request.Lifetime, request.Velocity, request.Scale, request.Seed) is true)
                return;

            // 状态提示也使用原版浮字；原版负责字体、缩放和存活时间。
            Rectangle location = new((int)request.WorldPosition.X, (int)request.WorldPosition.Y, 0, 0);
            CombatText.NewText(location, request.OverrideColor ?? TestModTextStyles.GetFallbackColor(request.StyleKey), request.Text, request.Crit);
        }

        public static void SpawnCombatText(Rectangle hitbox, int damage, bool crit, Color? color, string styleKey)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            if (Call("SpawnCombatText", hitbox, damage, crit, color, styleKey) is true)
                return;

            // 调用方已经隐藏原版伤害数字，此处恰好补画一次，保持原有 owner/联机边界。
            CombatText.NewText(hitbox, color ?? TestModTextStyles.GetFallbackColor(styleKey), damage, crit);
        }

        public static bool TryDrawTooltipLine(Item item, DrawableTooltipLine line, string styleKey) =>
            Call("TryDrawTooltipLine", item.type, line, styleKey) is true;

        public static bool TryDrawDamageLine(Item item, DrawableTooltipLine line)
        {
            DamageClass damageClass = item.DamageType;
            string key = null;
            if (damageClass == TrueDamageClass.Instance)
                key = TestModTextStyles.DamageTrue;
            else if (damageClass == DamageClass.Melee || damageClass == DamageClass.MeleeNoSpeed)
                key = TestModTextStyles.DamageMelee;
            else if (damageClass == DamageClass.Ranged)
                key = TestModTextStyles.DamageRanged;
            else if (damageClass == DamageClass.Magic)
                key = TestModTextStyles.DamageMagic;
            else if (damageClass == DamageClass.Summon || damageClass == DamageClass.SummonMeleeSpeed)
                key = TestModTextStyles.DamageSummon;
            return key is not null && TryDrawTooltipLine(item, line, key);
        }
    }
}
