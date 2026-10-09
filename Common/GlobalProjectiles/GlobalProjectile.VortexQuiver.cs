using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        private bool vortexArrow;
        private bool vortexHolyStar;
        private int vortexAmmoCategory;
        private bool vortexAmmoEmpowered;
        internal bool VortexHasLowGravity(Projectile p) => vortexArrow && p.arrow && p.type is
            ProjectileID.WoodenArrowFriendly or ProjectileID.FireArrow or ProjectileID.UnholyArrow or
            ProjectileID.HellfireArrow or ProjectileID.HolyArrow or ProjectileID.CursedArrow or
            ProjectileID.BoneArrow or ProjectileID.BoneArrowFromMerchant or ProjectileID.FrostburnArrow or
            ProjectileID.IchorArrow or ProjectileID.VenomArrow;

        private void VortexQuiver_OnSpawn(Projectile p, IEntitySource source)
        {
            if (p.owner != Main.myPlayer || p.owner < 0 || p.owner >= Main.maxPlayers ||
                p.hostile || p.npcProj || IsExtraHit || IsTimeEchoAttack) return;

            GlobalProjectile parentState = null;
            Projectile parent = (source as EntitySource_Parent)?.Entity as Projectile;
            if (parent != null && parent.owner == p.owner) parentState = parent.GetGlobalProjectile<GlobalProjectile>();
            Player owner = Main.player[p.owner];
            VortexQuiverPlayer state = null;
            if (owner.active) owner.TryGetModPlayer(out state);
            if (source is EntitySource_ItemUse use && use.Player.whoAmI == p.owner)
                vortexAmmoCategory = use.Item.useAmmo == AmmoID.Bullet || use.Item.useAmmo == AmmoID.Rocket
                    ? use.Item.useAmmo : 0;
            else if (parentState != null && !parentState.IsExtraHit && !parentState.IsTimeEchoAttack)
                vortexAmmoCategory = parentState.vortexAmmoCategory;

            if (vortexAmmoCategory != 0 && p.friendly && !p.minion && !p.sentry)
            {
                // 普通已发射弹幕保留快照；手持载体的新射击读取当前装备，不继承启动时状态。
                vortexAmmoEmpowered = parentState != null && !TimeEchoAttackSystem.IsResourceCarrier(parent)
                    ? parentState.vortexAmmoEmpowered : state?.IsEquipped(vortexAmmoCategory) == true;
                if (vortexAmmoEmpowered) p.netUpdate = true;
            }
            if (!p.friendly) return;
            if (p.type == ProjectileID.HallowStar && parentState != null &&
                ((parent.type == ProjectileID.HolyArrow && parentState.vortexArrow) ||
                 (parent.type == ProjectileID.HallowStar && parentState.vortexHolyStar)))
            {
                // 原版每次派生都会重新计算初速；每颗只在本地生成时翻倍，网络接收不再乘。
                vortexHolyStar = true;
                p.velocity *= 2f;
                p.netUpdate = true;
                return;
            }
            if (!p.arrow) return;
            if ((parentState?.vortexArrow == true) ||
                state?.IsEquipped(AmmoID.Arrow) == true)
            {
                vortexArrow = true;
                p.netUpdate = true;
            }
        }

        private void VortexQuiver_OnHitNPC(Projectile p, NPC target)
        {
            bool empowered = vortexAmmoEmpowered;
            if (vortexAmmoCategory != 0 && TimeEchoAttackSystem.IsResourceCarrier(p))
                empowered = p.owner >= 0 && p.owner < Main.maxPlayers &&
                    Main.player[p.owner].TryGetModPlayer(out VortexQuiverPlayer state) &&
                    state.IsEquipped(vortexAmmoCategory);
            if (!IsExtraHit && !IsTimeEchoAttack && ((vortexArrow && p.arrow) || empowered) &&
                p.friendly && !p.hostile && p.owner == Main.myPlayer)
                target.AddBuff(BuffID.Electrified, 300);
        }
    }
}
