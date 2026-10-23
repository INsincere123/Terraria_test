using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Content.Items.Weapons.Plus;
using TestMod.Content.Projectiles.Ranged;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        public PlusWeaponKind PlusWeapon { get; private set; }

        private static bool PlusWeaponMatches(PlusWeaponKind kind, int type)
            => PlusWeaponDefinition.MatchesProjectile(kind, type) || kind == PlusWeaponKind.Phantasm &&
                (type == ModContent.ProjectileType<PhantasmHoldout>() || type == ModContent.ProjectileType<PhantasmSpecialArrowProj>());

        private void PlusWeapon_OnSpawn(Projectile p, IEntitySource source)
        {
            if (IsTimeEchoAttack || IsExtraHit || p.hostile || p.npcProj || p.trap) return;
            PlusWeaponKind kind = PlusWeaponKind.None;
            if (source is EntitySource_ItemUse use && use.Player.whoAmI == p.owner && use.Item.ModItem is PlusWeapon weapon)
            {
                kind = weapon.Kind;
            }
            else if (source is EntitySource_Parent { Entity: Projectile parent } && parent.owner == p.owner)
            {
                var parentState = parent.GetGlobalProjectile<GlobalProjectile>();
                if (!parentState.IsExtraHit && !parentState.IsTimeEchoAttack) kind = parentState.PlusWeapon;
            }
            if (!PlusWeaponMatches(kind, p.type)) return;
            PlusWeapon = kind;
            // 原版仆从每次更新从 originalDamage 重算，不能只强化首次发射的 damage。
            if (source is EntitySource_ItemUse itemUse &&
                (p.minion || p.sentry || ProjectileID.Sets.StardustDragon[p.type]))
                p.originalDamage = (int)(itemUse.Item.damage * PlusWeaponDefinition.For(kind).WeaponDamage);
            if (p.owner == Main.myPlayer) p.netUpdate = true;
        }

        internal bool HasPlusWeapon(PlusWeaponKind kind) => PlusWeapon == kind && !IsTimeEchoAttack && !IsExtraHit;

        private void PlusWeapon_Read(Projectile p, byte kind, int originalDamage)
        {
            var value = (PlusWeaponKind)kind;
            PlusWeapon = !IsTimeEchoAttack && !IsExtraHit && PlusWeaponMatches(value, p.type) ? value : PlusWeaponKind.None;
            if (PlusWeapon != PlusWeaponKind.None && originalDamage >= 0 &&
                (p.minion || p.sentry || ProjectileID.Sets.StardustDragon[p.type]))
                p.originalDamage = originalDamage;
        }
    }
}
