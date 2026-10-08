using Terraria;
using Terraria.DataStructures;
using TestMod.Common.DataStructures;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // 来源只供本地攻击分流，不保存、不新增网络字段；各弹幕实例独立维护。
        private Item echoSourceItem;
        private int echoSourceItemType;
        internal bool EchoSourceUsesResources { get; private set; }
        internal bool EchoHasWeaponSource { get; private set; }
        internal bool EchoCompletedAI { get; private set; }
        internal bool EchoResourceCarrier { get; private set; }
        internal Item EchoSourceItem => echoSourceItem?.type == echoSourceItemType ? echoSourceItem : null;
        internal TimeEchoShotCoordinates? EchoSpawnCoordinates { get; private set; }
        internal bool EchoPreserveBubbleAmmo { get; private set; }
        private Projectile echoSourceParent;
        private int echoSourceParentIdentity, echoSourceParentType, echoSourceParentOwner;
        internal Projectile EchoSourceParent => echoSourceParent != null && echoSourceParent.active &&
            echoSourceParent.identity == echoSourceParentIdentity && echoSourceParent.type == echoSourceParentType &&
            echoSourceParent.owner == echoSourceParentOwner &&
            ReferenceEquals(Main.projectile[echoSourceParent.whoAmI], echoSourceParent) ? echoSourceParent : null;

        internal void SetEchoSpawnPolicy(TimeEchoShotCoordinates coordinates, bool preserveBubbleAmmo)
        { EchoSpawnCoordinates = coordinates; EchoPreserveBubbleAmmo = preserveBubbleAmmo; }

        private void RecordEchoSource(Projectile p, IEntitySource source)
        {
            if (source is EntitySource_ItemUse use && use.Player.whoAmI == p.owner)
            {
                echoSourceItem = use.Item;
                echoSourceItemType = use.Item.type;
                EchoSourceUsesResources = use.Item.useAmmo > 0 || use.Item.mana > 0;
                EchoHasWeaponSource = true;
            }
            else if (source is EntitySource_Parent { Entity: Projectile parent } && parent.owner == p.owner)
            {
                echoSourceParent = parent;
                echoSourceParentIdentity = parent.identity; echoSourceParentType = parent.type; echoSourceParentOwner = parent.owner;
                InheritEchoSource(parent.GetGlobalProjectile<GlobalProjectile>());
                if (source is IEntitySource_OnHit) EchoHasWeaponSource = false;
            }
            else if (source is TimeEchoAttackSource { Shot: { } shot })
                InheritEchoSource(shot.Entity.GetGlobalProjectile<GlobalProjectile>());
        }

        private void InheritEchoSource(GlobalProjectile parent)
        {
            echoSourceItem = parent.echoSourceItem;
            echoSourceItemType = parent.echoSourceItemType;
            EchoSourceUsesResources = parent.EchoSourceUsesResources;
            EchoHasWeaponSource = parent.EchoHasWeaponSource;
        }

        internal void ConfirmEchoCopyMode(bool resourceCarrier)
        {
            EchoCompletedAI = true;
            EchoResourceCarrier |= resourceCarrier;
        }
    }
}
