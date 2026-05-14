using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.Overlays
{
    /// <summary>
    /// 护盾 UI — 类英雄联盟风格，叠加在血量显示上。
    ///
    /// Classic / Fancy 模式（心形图标）：
    ///   在"已用完但护盾能补到"的空血心格上画半透明蓝心，
    ///   让玩家直观感受护盾等效于多少血。
    ///
    /// 横条 (HorizontalBars) 模式：
    ///   在血量条上叠加护盾覆盖层（浅蓝边框 + 近透明内部）。
    ///   宽度比例 = 当前护盾值 / 最大生命值。
    ///
    /// 原理：
    ///   PostDrawResource  — 每绘制完一颗心/星后回调；
    ///                       通过贴图类型区分血（心）和魔（星）。
    ///   PostDrawResourceDisplay — 整组绘制完毕后回调；
    ///                       用于横条模式的护盾条叠加。
    /// </summary>
    [Autoload(Side = ModSide.Client)]
    public class ShieldResourceOverlay : ModResourceOverlay
    {
        // Classic / Fancy 护盾心叠色
        private static readonly Color HeartShieldTint = new Color(80, 160, 255, 150);

        // 横条模式：护盾覆盖层颜色
        private static readonly Color BarFill   = new Color(150, 210, 255, 3);   // 内部近透明（薄层）
        private static readonly Color BarBorder = new Color(150, 210, 255, 210); // 外框清晰

        // ── 横条 HP 条位置常量（若 UI 错位请在此调整）────────────────────
        private const int HpBarX      = 0;    // 相对 Main.screenWidth - 306 的 X 偏移
        private const int HpBarY      = 21;   // HP 条在屏幕中的 Y 坐标（像素）
        private const int HpBarWidth  = 268;  // HP 条总宽度（像素）
        private const int HpBarHeight = 24;   // HP 条高度（像素）
        private const int BorderSize  = 3;    // 护盾边框厚度（像素）
        // ─────────────────────────────────────────────────────────────────

        // ════════════════════════════════════════════════════════════════
        //   PostDrawResource — Classic / Fancy：在护盾区心格上叠蓝心
        // ════════════════════════════════════════════════════════════════
        public override void PostDrawResource(ResourceOverlayDrawContext context)
        {
            // 通过贴图类型判断是否是血量心（Heart / Heart2）
            // 魔力星使用不同贴图，此判断可靠区分两者
            Texture2D tex = context.texture.Value;
            bool isLifeHeart = (tex == TextureAssets.Heart.Value)
                            || (tex == TextureAssets.Heart2.Value);
            if (!isLifeHeart) return;

            Player player = Main.LocalPlayer;
            ShieldPlayer sp = player.GetModPlayer<ShieldPlayer>();
            if (sp.MaxShield <= 0f || sp.CurrentShield <= 0f) return;

            // 此格代表的 HP 起始值（0-indexed resourceNumber）
            float lifePerHeart = context.snapshot.LifePerSegment;
            float heartStart   = context.resourceNumber * lifePerHeart;
            float life         = context.snapshot.Life;
            float shieldEnd    = life + sp.CurrentShield;

            // 护盾区：life ~ (life + shield) 之间的空格
            if (heartStart >= life && heartStart < shieldEnd)
            {
                context.SpriteBatch.Draw(
                    tex,
                    context.position,
                    context.source,
                    HeartShieldTint,
                    context.rotation,
                    context.origin,
                    context.scale,
                    context.effects,
                    0f);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //   PostDrawResourceDisplay — 横条模式：护盾层叠加在 HP 条上方
        //
        //   护盾条宽度 / HP条总宽度 = 当前护盾值 / 最大生命值
        //   视觉风格：浅蓝粗边框 + 内部近透明填充（能量容器感）
        // ════════════════════════════════════════════════════════════════
        public override void PostDrawResourceDisplay(
            PlayerStatsSnapshot snapshot,
            IPlayerResourcesDisplaySet displaySet,
            bool drawingLife, Color textColor, bool drawText)
        {
            if (!drawingLife) return;
            if (displaySet is not HorizontalBarsPlayerResourcesDisplaySet) return;

            Player player = Main.LocalPlayer;
            ShieldPlayer sp = player.GetModPlayer<ShieldPlayer>();
            if (sp.CurrentShield <= 0f) return;

            // 护盾条宽度 = (DisplayShield / MaxHP) × HP条总宽度
            // 使用平滑显示值，避免整数截断引起的每帧跳变感
            int shieldWidth = (int)(sp.DisplayShield / snapshot.LifeMax * HpBarWidth);
            if (shieldWidth < BorderSize * 2) return; // 太窄时不画（避免边框重叠）

            int x      = Main.screenWidth - 306 + HpBarX;
            int y      = HpBarY;
            int w      = shieldWidth;
            int h      = HpBarHeight;
            int b      = BorderSize;

            SpriteBatch sb    = Main.spriteBatch;
            Texture2D   pixel = TextureAssets.MagicPixel.Value;

            // 内部填充（近透明）
            sb.Draw(pixel, new Rectangle(x + b, y + b, w - b * 2, h - b * 2), BarFill);

            // 四条边框（清晰浅蓝）
            sb.Draw(pixel, new Rectangle(x,         y,         w, b), BarBorder); // 上
            sb.Draw(pixel, new Rectangle(x,         y + h - b, w, b), BarBorder); // 下
            sb.Draw(pixel, new Rectangle(x,         y,         b, h), BarBorder); // 左
            sb.Draw(pixel, new Rectangle(x + w - b, y,         b, h), BarBorder); // 右（护盾量末端）
        }
    }
}
