using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Content.Projectiles.TimeEcho;

namespace TestMod.Common.GlobalProjectiles
{
    public partial class GlobalProjectile
    {
        // 原实例到回放实例的单向绑定。副本销毁或穿透耗尽后，同一代攻击不能重新生成。
        private Projectile echoProjection;
        private int echoProjectionIdentity;
        private uint echoProjectionGeneration;
        private bool echoProjectionCreated;
        private Projectile echoBody;
        private int echoBodyIdentity, echoBodyType, echoBodyOwner;
        private uint echoBodyGeneration;

        internal void RecordEchoBody(Projectile body, uint generation)
        {
            echoBody = body; echoBodyIdentity = body.identity; echoBodyType = body.type;
            echoBodyOwner = body.owner; echoBodyGeneration = generation;
        }

        internal bool TryGetEchoCounterpart(uint generation, out Projectile body)
        {
            body = null;
            if (echoBody != null && echoBody.active && echoBodyGeneration == generation &&
                echoBody.identity == echoBodyIdentity && echoBody.type == echoBodyType && echoBody.owner == echoBodyOwner &&
                ReferenceEquals(Main.projectile[echoBody.whoAmI], echoBody) && echoBody.GetGlobalProjectile<GlobalProjectile>().IsTimeEchoAttack)
            { body = echoBody; return true; }
            if (echoProjectionGeneration == generation && IsEchoProjectionValid())
            { body = echoProjection; return true; }
            return false;
        }

        internal void UpdateEchoProjection(Projectile original, TimeEchoPlayer echo,
            TimeEchoAttackSystem.AttackAdapter adapter, uint root)
        {
            Projectile parent = EchoSourceParent;
            if (!original.active || !EchoHasWeaponSource || !echo.CanCopyAttack ||
                (adapter.RequiresParent && parent == null) ||
                (adapter.ParentRelative && (parent == null || !parent.GetGlobalProjectile<GlobalProjectile>().TryGetEchoCounterpart(echo.attackState.Generation, out _))) ||
                !TimeEchoAttackSystem.TryCaptureProjection(original, adapter, out TimeEchoProjectionFrame frame))
            { StopEchoProjection(); return; }
            if (!echoProjectionCreated || echoProjectionGeneration != echo.attackState.Generation)
            {
                StopEchoProjection();
                if (!echo.TryStartProjection()) return;
                echoProjectionCreated = true;
                echoProjectionGeneration = echo.attackState.Generation;
                var attack = new TimeEchoAttackSource(new(root, echoProjectionGeneration), original.DamageType,
                    original.CritChance, original.ArmorPenetration);
                int index = Projectile.NewProjectile(attack, echo.AttackOrigin, Microsoft.Xna.Framework.Vector2.Zero,
                    ModContent.ProjectileType<TimeEchoProjectionProjectile>(), original.damage, original.knockBack, original.owner);
                echoProjection = index < Main.maxProjectiles ? Main.projectile[index] : null;
                echoProjectionIdentity = echoProjection?.identity ?? -1;
                if (echoProjection?.ModProjectile is TimeEchoProjectionProjectile created)
                    created.Bind(original, echo, echoProjectionGeneration, adapter.OwnerRelative, adapter.ClipBeam,
                        adapter.ParentRelative, adapter.WorldTarget);
            }
            if (IsEchoProjectionValid() && echoProjection.ModProjectile is TimeEchoProjectionProjectile projection)
                projection.UpdateFrame(original, echo, frame);
        }

        private bool IsEchoProjectionValid() => echoProjection != null && echoProjection.active &&
            echoProjection.identity == echoProjectionIdentity &&
            ReferenceEquals(Main.projectile[echoProjection.whoAmI], echoProjection) &&
            echoProjection.ModProjectile is TimeEchoProjectionProjectile;

        internal void StopEchoProjection()
        {
            if (IsEchoProjectionValid())
            {
                echoProjection.active = false;
                if (Main.netMode == NetmodeID.MultiplayerClient)
                    NetMessage.SendData(MessageID.KillProjectile, number: echoProjection.identity, number2: echoProjection.owner);
            }
            echoProjection = null;
        }

        public override void OnKill(Projectile projectile, int timeLeft) => StopEchoProjection();
    }
}
