using Microsoft.Xna.Framework;
using Terraria;

namespace TestMod.Common.Mechanics.Lifetaking
{
    // 保留原弹幕的运动与寿命，不撤销目标伤害、免疫表、玩家效果或独立派生攻击。
    internal readonly struct LifetakingProjectileSnapshot
    {
        private readonly Vector2 position, velocity, oldVelocity;
        private readonly (float, float, float) ai, localAI;
        private readonly int identity, type, owner, damage, penetrate, maxPenetrate, timeLeft,
            direction, spriteDirection, width, height, alpha, numHits, numUpdates;
        private readonly float knockBack, rotation, scale;
        private readonly bool active, friendly, hostile, tileCollide, hide, netUpdate, reflected;

        internal LifetakingProjectileSnapshot(Projectile p)
        {
            position = p.position; velocity = p.velocity; oldVelocity = p.oldVelocity;
            ai = (p.ai[0], p.ai[1], p.ai[2]); localAI = (p.localAI[0], p.localAI[1], p.localAI[2]);
            identity = p.identity; type = p.type; owner = p.owner; damage = p.damage;
            penetrate = p.penetrate; maxPenetrate = p.maxPenetrate; timeLeft = p.timeLeft;
            direction = p.direction; spriteDirection = p.spriteDirection; width = p.width; height = p.height;
            alpha = p.alpha; numHits = p.numHits; numUpdates = p.numUpdates;
            knockBack = p.knockBack; rotation = p.rotation; scale = p.scale;
            active = p.active; friendly = p.friendly; hostile = p.hostile;
            tileCollide = p.tileCollide; hide = p.hide; netUpdate = p.netUpdate;
            reflected = p.reflected;
        }

        internal void Restore(Projectile p)
        {
            // 回调可能重新生成并复用原槽位；绝不能把新实体还原成旧弹幕。
            if (!ReferenceEquals(Main.projectile[p.whoAmI], p) || p.identity != identity || p.type != type || p.owner != owner) return;
            p.position = position; p.velocity = velocity; p.oldVelocity = oldVelocity;
            (p.ai[0], p.ai[1], p.ai[2]) = ai; (p.localAI[0], p.localAI[1], p.localAI[2]) = localAI;
            p.damage = damage; p.penetrate = penetrate; p.maxPenetrate = maxPenetrate; p.timeLeft = timeLeft;
            p.direction = direction; p.spriteDirection = spriteDirection; p.width = width; p.height = height;
            p.alpha = alpha; p.numHits = numHits; p.numUpdates = numUpdates;
            p.knockBack = knockBack; p.rotation = rotation; p.scale = scale;
            p.active = active; p.friendly = friendly; p.hostile = hostile;
            p.tileCollide = tileCollide; p.hide = hide; p.netUpdate = netUpdate;
            p.reflected = reflected;
        }
    }
}
