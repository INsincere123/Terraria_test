using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Compatibility;
using TestMod.Common.Mechanics.AccessoryEffects;
using TestMod.Common.Players;

namespace TestMod.Content.Projectiles.Accessories
{
    /// <summary>复用已结算的追加伤害，一次投送至指定 NPC，同时提供独立统计来源。</summary>
    public abstract class InstantExtraHitProjectile : ModProjectile
    {
        private NPC.HitInfo _hit;
        private ExtraHitConfig _config;
        private bool _prepared;
        private bool _hasHit;
        private int _damageDone;

        public override string Texture => "Terraria/Images/Projectile_0";

        public override void SetStaticDefaults()
            => ProjectileID.Sets.DontApplyParryDamageBuff[Type] = true;

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            // 单次穿透绕过既有共用免疫帧，也不写入或清空它；不使用 local/static 免疫。
            Projectile.penetrate = Projectile.maxPenetrate = 1;
            Projectile.appliesImmunityTimeOnSingleHits = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.noEnchantments = true;
            Projectile.hide = true;
            Projectile.timeLeft = 2;
        }

        public override void OnSpawn(IEntitySource source)
        {
            // ai[0] 是目标 NPC，ai[1] 是 DamageClass；首次生成包已有来源与递归保护。
            Projectile.DamageType = DamageClassLoader.GetDamageClass((int)Projectile.ai[1]) ?? DamageClass.Generic;
            Projectile.CritChance = 0;
            var state = Projectile.GetGlobalProjectile<global::TestMod.Common.GlobalProjectiles.GlobalProjectile>();
            state.IsExtraHit = true;
            // 暴击/穿防已在命中快照里结算，GlobalProjectile 不再随机判定第二次。
            state.ExtraHitUseCrit = false;
            state.ExtraHitIgnoreDefense = false;
        }

        public override bool? CanDamage()
            => _prepared && !_hasHit && Projectile.owner == Main.myPlayer && Main.netMode != NetmodeID.Server;

        public override bool? CanHitNPC(NPC target)
            => !_hasHit && target.whoAmI == (int)Projectile.ai[0] ? null : false;

        public override bool CanHitPvp(Player target) => false;
        public override bool ShouldUpdatePosition() => false;
        public override bool PreDraw(ref Color lightColor) => false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.DamageVariationScale *= 0f;
            modifiers.ModifyHitInfo += RestoreHit;
        }

        private void RestoreHit(ref NPC.HitInfo hit)
        {
            // 保留原入口结算的增伤、暴击、防御/抗性、方向、文字模式，不二次应用弹幕修正。
            hit = _hit;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            _hasHit = true;
            _damageDone = damageDone;
            // 数字由此处独占；GlobalProjectile 对瞬时追加弹幕不再补画真实伤害数字。
            if (!_config.UseVanillaCombatText)
                TextRenderingBridge.SpawnCombatText(target.Hitbox, hit.Damage, hit.Crit,
                    _config.CombatTextColor ?? ExtraHitEffect.DefaultCombatTextColor,
                    _config.CombatTextStyleKey ?? TestModTextStyles.ExtraHit);
        }

        internal static int Deliver<T>(Player player, NPC target, in NPC.HitInfo hit, in ExtraHitConfig config)
            where T : InstantExtraHitProjectile
        {
            int index = Projectile.NewProjectile(player.GetSource_Misc(typeof(T).Name), target.Center, Vector2.Zero,
                ModContent.ProjectileType<T>(), hit.SourceDamage, hit.Knockback, player.whoAmI,
                ai0: target.whoAmI, ai1: hit.DamageType.Type);
            if (index < 0 || index >= Main.maxProjectiles)
                return 0;

            Projectile projectile = Main.projectile[index];
            var delivery = (InstantExtraHitProjectile)projectile.ModProjectile;
            delivery._hit = hit;
            delivery._config = config;
            delivery._prepared = true;
            projectile.Center = target.Center;
            projectile.penetrate = projectile.maxPenetrate = 1;

            InstantExtraHitPlayer context = player.GetModPlayer<InstantExtraHitPlayer>();
            bool wasResolving = context.IsResolvingHit;
            context.IsResolvingHit = true;
            try
            {
                // owner 同帧命中。Damage 负责一次命中包，不能再额外 SendStrikeNPC。
                projectile.Damage();
                return delivery._damageDone;
            }
            finally
            {
                context.IsResolvingHit = wasResolving;
                delivery._prepared = false;
                // OnKill 可能生成其他弹幕并复用槽位，不能误杀替换后的实例。
                if (projectile.active && ReferenceEquals(projectile.ModProjectile, delivery))
                    projectile.Kill();
            }
        }
    }

    public class HeartsteelExtraHitProjectile : InstantExtraHitProjectile { }
    public class BerserkBladeExtraHitProjectile : InstantExtraHitProjectile { }
    public class FocusExtraHitProjectile : InstantExtraHitProjectile { }
    public class HarvestExtraHitProjectile : InstantExtraHitProjectile { }
}
