using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;

namespace TestMod.Projectiles
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
    /// 2. 贴图格式：竖向多帧，第 0 帧=手柄，中间帧=鞭身（可多帧），最后一帧=鞭尖。
    ///    帧数在 SetStaticDefaults 里用 Main.projFrames[Type] = N 设置。
    /// 3. Segments / RangeMultiplier / extraUpdates 根据自己贴图长度重新调整。
    /// </summary>
    public class TestWhipProj : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.IsAWhip[Type] = true;

            // 【换贴图时】在这里设置自己贴图的竖向帧数，例如：
            // Main.projFrames[Type] = 5; // 1帧手柄 + 3帧鞭身 + 1帧鞭尖
            // 目前借用暗黑收割（849）的帧数，不需要在这里设置。
        }

        public override void SetDefaults()
        {
            Projectile.DefaultToWhip();

            // 【换贴图时】根据贴图实际长度调整这三个参数：
            // Segments   = 节段数，越多鞭子越长，建议与贴图比例匹配
            // RangeMultiplier = 整体范围倍数
            // extraUpdates    = 每帧额外更新次数，影响挥鞭速度和鞭子弧度
            // 以下数值与原版暗黑收割（849）一致，换贴图后可自由调整。
            Projectile.WhipSettings.Segments = 30;
            Projectile.WhipSettings.RangeMultiplier = 2.15f;
            Projectile.extraUpdates = 2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(ModContent.BuffType<TestWhipTagBuff>(), 240);
            Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // ══════════════════════════════════════════════════════════
            // 【换贴图时】把下面两行：
            //   Main.instance.LoadProjectile(849);
            //   Texture2D texture = TextureAssets.Projectile[849].Value;
            //   int frameCount = Main.projFrames[849];
            // 替换为：
            //   Texture2D texture = TextureAssets.Projectile[Type].Value;
            //   int frameCount = Main.projFrames[Type];
            // 然后把同名 PNG 放在 Projectiles/ 目录下即可，其余绘制代码不用动。
            // ══════════════════════════════════════════════════════════

            Main.instance.LoadProjectile(849);
            Texture2D texture = TextureAssets.Projectile[849].Value;
            int frameCount = Main.projFrames[849];

            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);

            int segmentHeight = texture.Height / frameCount;
            Vector2 origin = new Vector2(texture.Width / 2f, segmentHeight / 2f);

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 pos = points[i] - Main.screenPosition;
                Vector2 toNext = points[i + 1] - points[i];
                float rotation = toNext.ToRotation() - MathHelper.PiOver2;
                Color color = Lighting.GetColor(points[i].ToTileCoordinates());

                // 帧选择：0=手柄，最后一段=鞭尖，中间循环鞭身帧
                int frameY;
                if (i == 0)
                    frameY = 0;
                else if (i == points.Count - 2)
                    frameY = frameCount - 1;
                else
                    frameY = 1 + ((i - 1) % Math.Max(1, frameCount - 2));

                Rectangle sourceRect = new Rectangle(0, frameY * segmentHeight, texture.Width, segmentHeight);

                Main.EntitySpriteDraw(
                    texture, pos, sourceRect, color,
                    rotation, origin, 1f, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}
