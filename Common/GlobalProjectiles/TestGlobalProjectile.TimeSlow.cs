using Microsoft.Xna.Framework;
using TestMod.Common.Players;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class TestGlobalProjectile : GlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   时缓 + 减速力场 弹幕速度处理
        //
        //   不能每帧直接 velocity *= factor：对只改方向不改速度大小的 AI
        //  （如 AI_173 彩虹弧线）会造成指数衰减→速度归零。
        //
        //   正确方案（save / restore，实例方法保证 this 一致）：
        //     PreAI  → 恢复上帧 AI 运行前的自然速度
        //     PostAI → AI 跑完后保存自然速度，再缩放
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 由主文件 PreAI 最开头调用。
        /// 若上帧在力场内，先把 velocity 恢复到自然值，让 AI 以正确速度运算。
        /// </summary>
        internal void SlowField_RestoreVelocity(Projectile projectile)
        {
            if (!_sfInField) return;
            if (!projectile.hostile || projectile.friendly) return;
            projectile.velocity = _sfNaturalVelocity;
        }

        /// <summary>
        /// 由主文件 PostAI 末尾调用（静态方法，通过 GetGlobalProjectile 访问实例字段）。
        /// 处理全局时缓和减速力场的速度缩放。
        /// </summary>
        internal static void ApplyTimeSlowToProjectile(Projectile projectile)
        {
            if (!projectile.hostile || projectile.friendly) return;

            // extraUpdates 会让 PostAI 每帧被调多次；只在最后一次（numUpdates == -1）处理
            if (projectile.numUpdates != -1) return;

            var self = projectile.GetGlobalProjectile<TestGlobalProjectile>();

            // ── 全局时缓（不做 save/restore，直接缩放）────────────
            if (Main.LocalPlayer.active)
            {
                var mp = Main.LocalPlayer.GetModPlayer<TimeStopPlayer>();
                if (mp.TimeSlowActive)
                {
                    self._sfInField = false;
                    projectile.velocity *= mp.TimeSlowFactor;
                    return;
                }
            }

            // ── 减速力场（range-based，save / restore 防衰减）────────
            if (SlowFieldPlayer.IsInAnySlowField(projectile.Center, out float factor))
            {
                self._sfNaturalVelocity = projectile.velocity;
                self._sfInField         = true;
                projectile.velocity    *= factor;
            }
            else
            {
                self._sfInField = false;
            }
        }
    }
}
