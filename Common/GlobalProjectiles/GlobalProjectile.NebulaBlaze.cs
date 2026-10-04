using Terraria;
//using Terraria.ID;
using Microsoft.Xna.Framework;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // 标记该弹射物是否已经完成了环射生成，避免每帧重复触发
        // 利用 localAI[2] 作为标记位（原版星云烈焰不使用 localAI[2]）
        private const float NebulaBlazeSpawnedFlag = 1f;

        // ══════════════════════════════════════════════════════════════
        //   星云烈焰强化追踪（沿用现有 2400px 通用范围）
        //   在 PostAI 分发处调用，普通弹和 Ex 弹均适用
        // ══════════════════════════════════════════════════════════════
        private void ApplyNebulaBlazeBoostedTracking(Projectile projectile)
        {
            // 飞出 45 帧后才开始追踪
            int ticksPerFrame = projectile.extraUpdates + 1;
            if (projectile.timeLeft > 3600 - 45 * ticksPerFrame)
                return;

            // 沿用 2400px 索敌范围，小幅提高追踪速度和转向响应。
            ApplyHighTierTracking(projectile, 18.4f, 25.3f, 0.0375f, 0.225f);
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
