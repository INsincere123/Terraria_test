using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;
using TestMod.Content.Projectiles.Accessories;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    /// <summary>
    /// "副手"饰品的追加攻击效果。
    /// 触发时：在玩家头顶生成（或复用）炮台弹幕 <see cref="SubhandCannon"/>，
    /// 并从炮台位置向目标发射一颗 MagnetSphereBolt。
    /// 通过 ExtraHitEffect.SpawnProjectile 统一计算并投送，比例伤害不再次增伤或暴击。
    /// </summary>
    public class SubhandOnHitEffect : OnHitEffect
    {
        // ======================================================
        //  调参区
        // ======================================================

        /// <summary>
        /// 穿透弹幕触发时的全局冷却帧数。
        /// 非穿透命中不受此限制。
        /// </summary>
        private const int GLOBAL_CD = 20;

        /// <summary>追加攻击伤害为触发伤害的百分比（1.0 = 100%）。</summary>
        private const float DAMAGE_RATIO = 0.19f;

        // 伤害配置：触发伤害的 19%，无属性加成（由弹幕自身应用 NPC 防御）
        private static readonly ExtraHitConfig DamageConfig = new()
        {
            HitDamageRatio = DAMAGE_RATIO,
            StatType       = PlayerStatType.None,
            FixedClass     = DamageClass.Magic,
            Knockback      = 2f,
        };

        // ======================================================
        //  构造
        // ======================================================

        public SubhandOnHitEffect() : base(globalCooldown: GLOBAL_CD) { }

        // ======================================================
        //  排除列表：MagnetSphereBolt 命中时不再触发追加攻击
        // ======================================================

        private static readonly int[] ExcludedTypes = [ProjectileID.MagnetSphereBolt];
        public override int[] ExcludedProjectileTypes => ExcludedTypes;

        // ======================================================
        //  触发逻辑
        // ======================================================

        public override void Trigger(Player player, NPC target, NPC.HitInfo hit, int damageDone, Projectile sourceProjectile)
        {
            // 只在本地玩家端生成弹幕
            if (player.whoAmI != Main.myPlayer)
                return;

            SubhandCannon cannon = FindOrSpawnCannon(player);
            cannon?.FireAt(target, DamageConfig, damageDone);
        }

        // ======================================================
        //  辅助：查找或生成炮台
        // ======================================================

        private static SubhandCannon FindOrSpawnCannon(Player player, string textureOverride = null)
        {
            int cannonType = ModContent.ProjectileType<SubhandCannon>();

            foreach (Projectile proj in ProjectileLookup.Owned(player.whoAmI, cannonType))
            {
                SubhandCannon cannon = (SubhandCannon)proj.ModProjectile;
                if (textureOverride != null)
                    cannon.TextureOverride = textureOverride;
                return cannon;
            }

            // 生成新炮台
            Vector2 spawnPos = player.Top + new Vector2(0f, -40f);
            int index = Projectile.NewProjectile(
                player.GetSource_Accessory(player.HeldItem),
                spawnPos,
                Vector2.Zero,
                cannonType,
                0,
                0f,
                player.whoAmI
            );

            if (index < Main.maxProjectiles)
            {
                SubhandCannon cannon = (SubhandCannon)Main.projectile[index].ModProjectile;
                cannon.TextureOverride = textureOverride;
                return cannon;
            }
            return null;
        }
        /// <summary>保持炮台存活，在装备的 UpdateAccessory / UpdateArmorSet 中每帧调用。</summary>
        public static void KeepCannonAlive(Player player)
        {
            int cannonType = ModContent.ProjectileType<SubhandCannon>();
            foreach (Projectile proj in ProjectileLookup.Owned(player.whoAmI, cannonType))
            {
                proj.timeLeft = 2;
                break;
            }
        }
    }
}
