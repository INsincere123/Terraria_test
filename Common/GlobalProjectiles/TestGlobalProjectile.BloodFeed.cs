using Terraria;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Common.GlobalProjectiles
{
    // 暴走期间的弹幕增强：无限穿透 + 无视防御
    public partial class TestGlobalProjectile : GlobalProjectile
    {
        // 由主文件 PreAI 调用，确保在碰撞检测前设置穿透
        private void BloodFeed_SetBerserkPenetrate(Projectile projectile)
        {
            if (!projectile.friendly || projectile.hostile) return;
            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers) return;

            Player owner = Main.player[projectile.owner];
            if (!owner.active) return;

            var mp = owner.GetModPlayer<BloodFeedPlayer>();
            if (!mp.IsBerserk) return;

            if (projectile.penetrate != -1)
                projectile.penetrate = -1;
        }
    }
}
