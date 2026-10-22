using Terraria;
using Terraria.DataStructures;
using TestMod.Common.DataStructures;
using TestMod.Content.Items.Weapons.Plus;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // 独立于强化倍率的来源位：不限制弹幕 type，避免派生攻击漏入神工开物。
        internal bool ExcludedFromGodSmith;

        internal static bool MarkGodSmithIsolation(Projectile projectile, IEntitySource source)
        {
            if (projectile == null || !projectile.TryGetGlobalProjectile(out GlobalProjectile state)) return false;
            bool excluded = source switch
            {
                EntitySource_ItemUse use => use.Player.whoAmI == projectile.owner && use.Item.ModItem is PlusWeapon,
                EntitySource_Parent { Entity: Projectile parent } => parent.owner == projectile.owner && IsGodSmithIsolated(parent),
                TimeEchoAttackSource { Shot: { Entity: { } original } } => IsGodSmithIsolated(original),
                _ => false
            };
            // 外部 OnSpawn 可以先于本模组，重复查询只补标、不清除已确定的来源。
            state.ExcludedFromGodSmith |= excluded;
            return state.ExcludedFromGodSmith;
        }

        internal static bool IsGodSmithIsolated(Projectile projectile)
            => projectile != null && projectile.TryGetGlobalProjectile(out GlobalProjectile state) &&
                (state.ExcludedFromGodSmith || state.PlusWeapon != PlusWeaponKind.None);
    }
}
