using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Mechanics.Dashes;

namespace TestMod.Content.Projectiles.Dashes
{
    /// <summary>命名的瞬时冲刺伤害；同步的是来源实体，伤害仅由 owner 当场投送。</summary>
    public abstract class DashStrikeProjectile : ModProjectile
    {
        private NPC.HitInfo _contactHit;
        private bool _prepared;
        private bool _hasHit;

        public override string Texture => "Terraria/Images/Projectile_0";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DontApplyParryDamageBuff[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            // 单次穿透既不受 owner 共用免疫帧阻挡，也不写入新的共用免疫帧。
            Projectile.penetrate = Projectile.maxPenetrate = 1;
            Projectile.appliesImmunityTimeOnSingleHits = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
            Projectile.noEnchantments = true;
            Projectile.timeLeft = 2;
        }

        public override void OnSpawn(IEntitySource source)
        {
            // ai[0] = 指定 NPC；ai[1] = 伤害类型。首次生成包就携带完整来源标记。
            Projectile.DamageType = DamageClassLoader.GetDamageClass((int)Projectile.ai[1]) ?? DamageClass.Melee;
            Projectile.CritChance = 0;
            Projectile.GetGlobalProjectile<global::TestMod.Common.GlobalProjectiles.GlobalProjectile>().IsExtraHit = true;
        }

        public override bool? CanDamage()
            => _prepared && !_hasHit && Projectile.owner == Main.myPlayer && Main.netMode != NetmodeID.Server;

        public override bool? CanHitNPC(NPC target)
            => target.whoAmI == (int)Projectile.ai[0] && !_hasHit ? null : false;

        public override bool CanHitPvp(Player target) => false;
        public override bool ShouldUpdatePosition() => false;
        public override bool PreDraw(ref Color lightColor) => false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // 原冲刺已经完成防御、抗性和固定概率暴击结算。还原结果，避免新流程二次修正。
            modifiers.DamageVariationScale *= 0f;
            modifiers.ModifyHitInfo += RestoreContactHit;
        }

        private void RestoreContactHit(ref NPC.HitInfo hit)
        {
            bool hideCombatText = hit.HideCombatText;
            hit = _contactHit;
            hit.HideCombatText |= hideCombatText;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 不在回调中销毁：其他模组还需要读取本次命中的弹幕来源。
            _hasHit = true;
        }

        internal static bool Strike<T>(Player player, NPC target, in NPC.HitInfo hit, string sourceContext)
            where T : DashStrikeProjectile
        {
            if (Main.netMode == NetmodeID.Server || player.whoAmI != Main.myPlayer)
                return false;

            int index = Projectile.NewProjectile(player.GetSource_Misc(sourceContext), target.Center, Vector2.Zero,
                ModContent.ProjectileType<T>(), hit.SourceDamage, hit.Knockback, player.whoAmI,
                ai0: target.whoAmI, ai1: hit.DamageType.Type);
            if (index < 0 || index >= Main.maxProjectiles)
                return false;

            Projectile projectile = Main.projectile[index];
            var strike = (DashStrikeProjectile)projectile.ModProjectile;
            strike._contactHit = hit;
            strike._prepared = true;
            projectile.Center = target.Center;
            projectile.penetrate = projectile.maxPenetrate = 1;

            DashPlayer dash = player.GetModPlayer<DashPlayer>();
            bool wasResolving = dash.IsResolvingContactDamage;
            dash.IsResolvingContactDamage = true;
            try
            {
                // 同帧命中，避免盾击结束、NPC 移动或下一次冲刺改变本次碰撞结果。
                // Damage 自己发送命中包；这里不能再手动 SendStrikeNPC。
                projectile.Damage();
                return strike._hasHit;
            }
            finally
            {
                dash.IsResolvingContactDamage = wasResolving;
                strike._prepared = false;
                if (projectile.active) projectile.Kill();
            }
        }
    }

    public class StandardDashStrike : DashStrikeProjectile { }
    public class LongDashStrike : DashStrikeProjectile { }
    public class ShortDashStrike : DashStrikeProjectile { }
}
