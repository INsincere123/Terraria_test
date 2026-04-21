using Terraria;
//using Terraria.ID;
using Microsoft.Xna.Framework;

namespace 武器test
{
    public partial class MyGlobalProjectile
    {
        // 标记该弹射物是否已经完成了环射生成，避免每帧重复触发
        // 利用 localAI[2] 作为标记位（原版星云烈焰不使用 localAI[2]）
        private const float NebulaBlazeSpawnedFlag = 1f;

        // ══════════════════════════════════════════════════════════════
        //   星云烈焰强化追踪（800px 范围）
        //   在 PostAI 分发处调用，普通弹和 Ex 弹均适用
        // ══════════════════════════════════════════════════════════════
        private void ApplyNebulaBlazeBoostedTracking(Projectile projectile)
        {
            // 飞出 45 帧后才开始追踪
            int ticksPerFrame = projectile.extraUpdates + 1;
            if (projectile.timeLeft > 3600 - 45 * ticksPerFrame)
                return;

            // 追踪范围扩大到 800px，其余参数保持和原版手感接近
            // minSpeed=16, maxSpeed=22 对应原版约 16~20 速度区间
            ApplyHighTierTracking(projectile, 16f, 22f, 0.03f, 0.18f);
        }

        // ══════════════════════════════════════════════════════════════
        //   星云烈焰环射生成：生成第一帧时额外发射 7 个方向的副本
        //   模仿 NuclearFury 的 8 方向环形扩散
        // ══════════════════════════════════════════════════════════════
        private void TrySpawnNebulaBlazeRing(Projectile projectile)
        {
            if (Main.myPlayer != projectile.owner) return;
            if (projectile.localAI[2] == NebulaBlazeSpawnedFlag) return;

            projectile.localAI[2] = NebulaBlazeSpawnedFlag;

            float baseAngle = projectile.velocity.ToRotation();
            float speed = projectile.velocity.Length();

            for (int i = 1; i < 8; i++)
            {
                float angle = baseAngle + MathHelper.TwoPi * i / 8f;
                Vector2 newVel = angle.ToRotationVector2() * speed;

                int index = Projectile.NewProjectile(
                    projectile.GetSource_FromThis(),
                    projectile.Center,
                    newVel,
                    projectile.type,
                    projectile.damage,
                    projectile.knockBack,
                    projectile.owner
                );

                // 生成后立刻设置副本标记，阻止它再次环射
                if (index >= 0 && index < Main.maxProjectiles)
                    Main.projectile[index].localAI[2] = NebulaBlazeSpawnedFlag;
            }
        }
    }
}
