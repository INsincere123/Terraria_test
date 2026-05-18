using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;
using TestMod.Common.Systems;
using TestMod.Projectiles.Summon;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class TestGlobalProjectile : GlobalProjectile
    {
        // ══════════════════════════════════════════════════════════════
        //   WhipTag — 鞭子 tag 效果系统
        //
        //   从 WhipTagRegistry 读取每个 tag 的参数，不硬编码任何数值。
        //   注册新鞭子只需在 WhipTagRegistry.PostSetupContent() 里加一行。
        //   由主文件的 ModifyHitNPC / OnHitNPC 调用，不依赖 godMode。
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// 由主文件 ModifyHitNPC 调用。
        /// 遍历注册表，对目标身上存在的所有 tag 叠加伤害和暴击效果。
        /// </summary>
        internal void WhipTag_ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (!IsSummonProjectile(projectile)) return;

            foreach (var (buffType, data) in WhipTagRegistry.Tags)
            {
                if (!target.HasBuff(buffType)) continue;

                if (data.FlatDamage > 0f)
                    modifiers.FlatBonusDamage += data.FlatDamage;

                if (data.CritChance > 0 && Main.rand.Next(100) < data.CritChance)
                    modifiers.SetCrit();
            }
        }

        /// <summary>
        /// 由主文件 OnHitNPC 调用。
        /// 遍历注册表，触发目标身上存在的 tag 的爆炸效果（带冷却）。
        /// </summary>
        internal void WhipTag_OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!IsSummonProjectile(projectile)) return;

            foreach (var (buffType, data) in WhipTagRegistry.Tags)
            {
                if (!data.HasExplosion) continue;
                if (!target.HasBuff(buffType)) continue;

                // 每个 tag 有独立的冷却 buff（用 buffType 区分）
                // 此处统一用 TestWhipCooldownBuff 作为冷却标记
                // 如需多鞭子各自独立冷却，可为每个 tag 单独创建冷却 buff
                if (target.HasBuff(ModContent.BuffType<TestWhipCooldownBuff>())) continue;

                Projectile.NewProjectile(
                    projectile.GetSource_FromThis(),
                    target.Center,
                    Microsoft.Xna.Framework.Vector2.Zero,
                    ModContent.ProjectileType<TestWhipExplosionProj>(),
                    (int)(damageDone * data.ExplosionDamageRatio),
                    0f,
                    projectile.owner
                );

                target.AddBuff(ModContent.BuffType<TestWhipCooldownBuff>(), data.CooldownFrames);
            }
        }

        // ──────────────────────────────────────────────────────────────
        //   工具方法：判断是否为召唤物弹射物
        // ──────────────────────────────────────────────────────────────
        private static bool IsSummonProjectile(Projectile projectile)
        {
            return projectile.minion
                || ProjectileID.Sets.MinionShot[projectile.type]
                || projectile.sentry
                || ProjectileID.Sets.SentryShot[projectile.type];
        }
    }
}
