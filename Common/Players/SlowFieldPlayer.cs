using System;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 减速力场系统。
    /// 饰品每帧设置 HasSlowField = true 来激活；
    /// 全局钩子（GlobalNPC / GlobalProjectile）通过 IsInAnySlowField 检测范围并缩速。
    /// </summary>
    public class SlowFieldPlayer : ModPlayer
    {
        // ── 调参区 ────────────────────────────────────────────────
        /// <summary>圆环描边宽度（像素）</summary>
        private const float RingThickness = 4f;

        /// <summary>圆环基础不透明度（0~1）</summary>
        private const float RingAlpha = 0.35f;

        /// <summary>呼吸动画的透明度浮动幅度（叠加在 RingAlpha 上，±BreathRange）</summary>
        private const float BreathRange = 0.12f;

        /// <summary>呼吸动画速度（值越大越快）</summary>
        private const float BreathSpeed = 2.5f;

        /// <summary>圆环基础颜色（淡青紫，柔和不刺眼）</summary>
        private static readonly Color RingColor = new Color(160, 200, 255);

        /// <summary>圆环分段数（越高越圆滑）</summary>
        private const int RingSegments = 128;

        // ── 每帧由饰品写入 ─────────────────────────────────────
        public bool  HasSlowField;
        public float SlowFieldRadius = 320f;  // 像素（1 格 = 16px，320 ≈ 20 格）
        public float SlowFactor      = 0.25f; // 敌人/弹幕降至 25% 速度

        public override void ResetEffects()
        {
            HasSlowField = false;
        }

        // ── 范围检测（静态，供全局钩子调用）──────────────────────
        /// <summary>
        /// 检查世界坐标 <paramref name="worldPos"/> 是否在任意玩家的减速力场内。
        /// 如果是，输出该力场的减速系数并返回 true。
        /// </summary>
        public static bool IsInAnySlowField(Vector2 worldPos, out float slowFactor)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (!p.active) continue;

                var sfp = p.GetModPlayer<SlowFieldPlayer>();
                if (!sfp.HasSlowField) continue;

                if (Vector2.Distance(p.Center, worldPos) <= sfp.SlowFieldRadius)
                {
                    slowFactor = sfp.SlowFactor;
                    return true;
                }
            }

            slowFactor = 1f;
            return false;
        }

        // ── 力场圆环绘制 ──────────────────────────────────────────
        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (!HasSlowField) return;
            if (Main.dedServ)  return;

            DrawRingOutline();
        }

        private void DrawRingOutline()
        {
            // 呼吸感：sin 在 -1~1 之间振荡，映射到 [RingAlpha-BreathRange, RingAlpha+BreathRange]
            float breath = MathF.Sin(Main.GlobalTimeWrappedHourly * BreathSpeed);
            float currentAlpha = RingAlpha + breath * BreathRange;

            Color ringColor = RingColor * currentAlpha;

            var settings = new PrimitiveSettingsCircleEdge(
                EdgeWidthFunction: _ => RingThickness,
                ColorFunction:     _ => ringColor,
                RadiusFunction:    _ => SlowFieldRadius
            );

            PrimitiveRenderer.RenderCircleEdge(Player.Center, settings, totalPoints: RingSegments);
        }
    }
}