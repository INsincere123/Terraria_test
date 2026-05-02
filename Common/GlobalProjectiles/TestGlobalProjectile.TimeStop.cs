using TestMod.Common.Players;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.GlobalProjectiles
{
    /// <summary>
    /// 时停期间冻结敌方弹幕的辅助方法。
    /// 由于主文件 TestGlobalProjectile.cs 已经重写了 PreAI（用于乌鸦 AI 接管），
    /// 这里不再单独 override PreAI，而是提供 partial 辅助方法供主 PreAI 调用。
    ///
    /// 集成方式：在主文件 PreAI 最开头插入：
    ///     if (TryFreezeOnTimeStop(projectile)) return false;
    /// </summary>
    public partial class TestGlobalProjectile : GlobalProjectile
    {
        /// <summary>
        /// 判断弹幕是否应在时停期间被冻结。
        /// 仅冻结纯敌方弹幕（hostile == true 且 friendly == false）。
        /// 玩家弹幕、玩家召唤物、玩家鞭子标 friendly，自然跳过。
        /// </summary>
        private static bool ShouldFreezeProjectile(Projectile projectile)
        {
            if (!projectile.hostile) return false;
            if (projectile.friendly) return false;

            if (!Main.LocalPlayer.active) return false;
            TimeStopPlayer modPlayer = Main.LocalPlayer.GetModPlayer<TimeStopPlayer>();
            return modPlayer.TimeStopActive;
        }

        /// <summary>
        /// 若需要冻结此弹幕，则回滚位置/速度并锁动画，返回 true。
        /// 调用方应在 true 时立即 return false 跳过 AI。
        /// </summary>
        public static bool TryFreezeOnTimeStop(Projectile projectile)
        {
            if (!ShouldFreezeProjectile(projectile))
                return false;

            projectile.position = projectile.oldPosition;
            projectile.velocity = projectile.oldVelocity;
            projectile.frameCounter = 0;
            // 阻止时停期间弹幕自然过期消失
            projectile.timeLeft++;
            return true;
        }
    }
}
