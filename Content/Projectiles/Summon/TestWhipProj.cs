using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;

namespace TestMod.Content.Projectiles.Summon
{
    /// <summary>
    /// 测试鞭子弹射物。
    /// 鞭身节段在 PreDraw 里复用原版暗黑收割（ProjectileID 849）的贴图绘制。
    /// 鱼线由 vanilla 绘制系统自动画（白色，无法直接改色）。
    /// 命中后给敌人施加 TestWhipTagBuff，供全局钩子触发爆炸效果。
    ///
    /// ── 换成自己贴图时需要改的地方 ──
    /// 1. 删除 PreDraw 方法里的 LoadProjectile(849) 和 TextureAssets.Projectile[849]，
    ///    改用 TextureAssets.Projectile[Type]（自动读取本弹射物的同名 PNG）。
    /// 2. 贴图格式：
    ///    竖向（默认）：从上到下，第 0 帧=手柄，中间帧=鞭身（可多帧），最后一帧=鞭尖。
    ///    横向：从左到右，排列顺序同上。
    ///    帧数在 SetStaticDefaults 里用 Main.projFrames[Type] = N 设置。
    /// 3. Segments / RangeMultiplier / extraUpdates 根据自己贴图长度重新调整。
    /// </summary>
    public class TestWhipProj : ModProjectile
    {
        // ══════════════════════════════════════════════════════════
        // 绘制开关
        // ══════════════════════════════════════════════════════════
        private readonly bool drawHandle = false;         // 是否绘制手柄
        private readonly bool drawBody   = false;         // 是否绘制鞭身
        private readonly bool drawTip    = false;        // 是否绘制鞭尖

        /// <summary>
        /// 贴图帧排列方向。
        /// true  = 竖向（默认，推荐）：帧从上到下排列，每帧高度 = texture.Height / frameCount。
        /// false = 横向：帧从左到右排列，每帧宽度 = texture.Width / frameCount。
        /// </summary>
        private readonly bool verticalFrames = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.IsAWhip[Type] = true;

            // 【换贴图时】在这里设置自己贴图的帧数，例如：
            // Main.projFrames[Type] = 5; // 1帧手柄 + 3帧鞭身 + 1帧鞭尖
            // 目前借用暗黑收割（849）的帧数，不需要在这里设置。
        }

        public override void SetDefaults()
        {
            Projectile.DefaultToWhip();
            Projectile.width = 32;  
            Projectile.height = 32;
            // 【换贴图时】根据贴图实际长度调整这三个参数：
            // Segments        = 节段数，越多鞭子越长，建议与贴图比例匹配
            // RangeMultiplier = 整体范围倍数
            // extraUpdates    = 每帧额外更新次数，影响挥鞭速度和鞭子弧度
            // 以下数值与原版暗黑收割（849）一致，换贴图后可自由调整。
            Projectile.WhipSettings.Segments = 3;
            Projectile.WhipSettings.RangeMultiplier =6.2f;
            Projectile.extraUpdates = 1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(ModContent.BuffType<TestWhipTagBuff>(), 600); // 施加自定义标记buff，持续10秒（600帧）
            Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // ══════════════════════════════════════════════════════════
            // 【换贴图时】把下面三行：
            //   Main.instance.LoadProjectile(849);
            //   Texture2D texture = TextureAssets.Projectile[849].Value;
            //   int frameCount = Main.projFrames[849];
            // 替换为：
            //   Texture2D texture = TextureAssets.Projectile[Type].Value;
            //   int frameCount = Main.projFrames[Type];
            // 然后把同名 PNG 放在 Content/Projectiles/Summon/ 目录下即可，其余绘制代码不用动。
            // ══════════════════════════════════════════════════════════

            Main.instance.LoadProjectile(849);                          // 加载原版暗黑收割的贴图（ProjectileID 849）
            Texture2D texture = TextureAssets.Projectile[849].Value;   // 获取贴图对象
            int frameCount = Main.projFrames[849];                      // 获取帧数

            List<Vector2> points = new List<Vector2>();                 // 创建控制点列表
            Projectile.FillWhipControlPoints(Projectile, points);       // 填充鞭子的控制点（定义弧线路径）

            // 根据排列方向计算单帧尺寸和绘制原点
            int frameW, frameH;
            if (verticalFrames)
            {
                // 竖向：帧沿 Y 轴堆叠，每帧高度等分，宽度为整张贴图宽
                frameW = texture.Width;
                frameH = texture.Height / frameCount;
            }
            else
            {
                // 横向：帧沿 X 轴排列，每帧宽度等分，高度为整张贴图高
                frameW = texture.Width / frameCount;
                frameH = texture.Height;
            }

            Vector2 origin = new Vector2(frameW / 2f, frameH / 2f);    // 设置绘制原点（单帧中心）

            for (int i = 0; i < points.Count - 1; i++)                 // 遍历每对相邻控制点，绘制鞭子段
            {
                // 根据开关决定是否跳过绘制该段
                Color color = Lighting.GetColor(points[i].ToTileCoordinates());
                color = Color.Lerp(color, Color.White, 0.5f);
                if ((i == 0 && !drawHandle) ||                          // 跳过手柄
                    (i == points.Count - 2 && !drawTip) ||             // 跳过鞭尖
                    (i > 0 && i < points.Count - 2 && !drawBody))      // 跳过鞭身
                    color = Color.Transparent;

                Vector2 pos      = points[i] - Main.screenPosition;    // 计算屏幕坐标
                Vector2 toNext   = points[i + 1] - points[i];          // 计算到下一点的向量
                float rotation   = toNext.ToRotation() - MathHelper.PiOver2;           // 计算旋转角度（垂直于方向）

                // 帧索引：0=手柄，最后=鞭尖，中间循环鞭身帧
                int frameIndex;
                if (i == 0)
                    frameIndex = 0;
                else if (i == points.Count - 2)
                    frameIndex = frameCount - 1;
                else
                    frameIndex = 1 + ((i - 1) % Math.Max(1, frameCount - 2));

                // 根据排列方向构造裁剪矩形
                Rectangle sourceRect = verticalFrames
                    ? new Rectangle(0,          frameIndex * frameH, frameW, frameH)   // 竖向：偏移 Y
                    : new Rectangle(frameIndex * frameW, 0, frameW, frameH);           // 横向：偏移 X

                // EntitySpriteDraw 用法：绘制精灵 (贴图, 位置, 源矩形, 颜色, 旋转, 原点, 缩放, 效果, 层深度)
                Main.EntitySpriteDraw(
                    texture, pos, sourceRect, color,
                    rotation, origin, 1f, SpriteEffects.None, 0);
            }

            return true;   // 返回false，阻止默认绘制
        }
    }
}
