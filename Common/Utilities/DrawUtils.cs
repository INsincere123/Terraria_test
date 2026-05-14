using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace TestMod.Common.Utilities
{
    /// <summary>
    /// 无状态渲染工具：描边、Additive 混合切换、粒子线。
    /// 所有方法均为纯渲染操作，不修改游戏逻辑状态。
    /// </summary>
    public static class DrawUtils
    {
        // ══════════════════════════════════════════════════════════════
        //   SpriteBatch 混合模式切换
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 将 SpriteBatch 切换到 Additive（叠加发光）模式。
        /// 调用前 SpriteBatch 应处于任意 Begin 状态；调用后可绘制发光层。
        /// </summary>
        public static void BeginAdditive(SpriteBatch sb)
        {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                Main.DefaultSamplerState, null, null, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 将 SpriteBatch 恢复到标准 AlphaBlend + Deferred 模式。
        /// 在 BeginAdditive 之后调用以结束发光绘制。
        /// </summary>
        public static void EndAdditive(SpriteBatch sb)
        {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, null, null, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        // ══════════════════════════════════════════════════════════════
        //   描边绘制
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 以四向偏移方式绘制纹理描边。
        /// pos 为屏幕坐标（已减去 Main.screenPosition）。
        /// </summary>
        public static void DrawOutline(SpriteBatch sb, Texture2D tex,
            Vector2 pos, float rotation, float scale, Vector2 origin,
            Color color, float thickness = 2f)
        {
            sb.Draw(tex, pos + new Vector2( thickness,         0), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
            sb.Draw(tex, pos + new Vector2(-thickness,         0), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
            sb.Draw(tex, pos + new Vector2(        0,  thickness), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
            sb.Draw(tex, pos + new Vector2(        0, -thickness), null, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }

        // ══════════════════════════════════════════════════════════════
        //   粒子线
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 在 from→to 之间生成等距粒子，产生链式电弧/射线视觉效果。
        /// dustType 默认为电弧粒子（DustID.Electric）。
        /// 仅在客户端执行，服务端自动跳过。
        /// </summary>
        public static void SpawnDustLine(Vector2 from, Vector2 to, Color color,
            int dustType = DustID.Electric, int count = 6, float scale = 1.1f)
        {
            if (Main.netMode == NetmodeID.Server) return;

            Dust.QuickDustLine(from, to, 10f, color);

            for (int i = 0; i < count; i++)
            {
                // count=1 时避免除以零
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                Vector2 pos = Vector2.Lerp(from, to, t);
                Dust.NewDustPerfect(pos, dustType, Vector2.Zero, 0, color, scale);
            }

            // 终点额外一个亮点，强调命中感
            Dust.NewDustPerfect(to, dustType, Vector2.Zero, 0, Color.White, scale + 0.3f);
        }
    }
}
