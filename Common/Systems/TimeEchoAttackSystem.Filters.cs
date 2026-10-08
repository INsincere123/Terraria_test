using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Projectiles.TimeEcho;

namespace TestMod.Common.Systems
{
    public sealed partial class TimeEchoAttackSystem
    {
        // 普通直接攻击默认放行；这里只列无法独立运行或会改变玩家/世界的行为。
        private static readonly HashSet<int> excludedStyles = new()
        {
            ProjAIStyleID.Hook, ProjAIStyleID.MagicMissile,
            ProjAIStyleID.Flail, ProjAIStyleID.Drill,
            ProjAIStyleID.IceRod, ProjAIStyleID.Pet, ProjAIStyleID.FallingTile, ProjAIStyleID.Powder,
            ProjAIStyleID.GraveMarker, ProjAIStyleID.Spray, ProjAIStyleID.RopeCoil, ProjAIStyleID.FireWork,
            ProjAIStyleID.ExplosiveBunny, ProjAIStyleID.Heal, ProjAIStyleID.Bobber,
            ProjAIStyleID.ThickLaser, ProjAIStyleID.CoinPortal,
            ProjAIStyleID.Yoyo, ProjAIStyleID.PortalGate, ProjAIStyleID.SleepyOctopod,
            ProjAIStyleID.GolfBall, ProjAIStyleID.GolfClub,
            ProjAIStyleID.SuperStarBeam, ProjAIStyleID.LifeDrain,
            ProjAIStyleID.FlameThrower,
            ProjAIStyleID.Flamethrower
        };
        // AI 75 混有直接近战、选弹/耗魔载体和工具；只复制近战判定，资源载体只捕获实际射弹。
        private static readonly HashSet<int> resourceHeldTypes = new()
        {
            ProjectileID.LaserMachinegun, ProjectileID.LaserDrill, ProjectileID.ChargedBlasterCannon,
            ProjectileID.PortalGun, ProjectileID.VortexBeater, ProjectileID.Phantasm, ProjectileID.LastPrism,
            ProjectileID.DD2PhoenixBow, ProjectileID.Celeb2Weapon
        };
        internal static bool IsDirectHeldAttack(Projectile p) => p.aiStyle == ProjAIStyleID.HeldProjectile &&
            p.CountsAsClass(DamageClass.Melee) && !resourceHeldTypes.Contains(p.type) &&
            !(p.ModProjectile is { AIType: > 0 } mod && resourceHeldTypes.Contains(mod.AIType));
        internal static bool UsesEchoOwner(Projectile p) => p.aiStyle == ProjAIStyleID.Boomerang ||
            p.aiStyle == ProjAIStyleID.Harpoon || p.aiStyle == ProjAIStyleID.Spear ||
            p.aiStyle == ProjAIStyleID.ShortSword || p.aiStyle == ProjAIStyleID.Zenith || IsDirectHeldAttack(p);

        private static readonly HashSet<int> excludedVanillaTypes = new()
        {
            ProjectileID.VampireKnife, ProjectileID.VampireHeal, ProjectileID.SpiritHeal,
            ProjectileID.LovePotion, ProjectileID.FoulPotion,
            ProjectileID.TreeGlobe, ProjectileID.WorldGlobe, ProjectileID.MoonGlobe,
            ProjectileID.HolyWater, ProjectileID.UnholyWater, ProjectileID.BloodWater,
            ProjectileID.PortalGun, ProjectileID.PortalGunBolt, ProjectileID.PortalGunGate,
            ProjectileID.Grenade, ProjectileID.StickyGrenade, ProjectileID.BouncyGrenade, ProjectileID.PartyGirlGrenade,
            ProjectileID.Bomb, ProjectileID.StickyBomb, ProjectileID.BouncyBomb, ProjectileID.BombFish,
            ProjectileID.Dynamite, ProjectileID.StickyDynamite, ProjectileID.BouncyDynamite,
            ProjectileID.ScarabBomb, ProjectileID.WetBomb, ProjectileID.LavaBomb, ProjectileID.HoneyBomb,
            ProjectileID.DryBomb, ProjectileID.DirtBomb, ProjectileID.DirtStickyBomb,
            ProjectileID.Explosives, ProjectileID.TNTBarrel,
            ProjectileID.GrenadeII, ProjectileID.RocketII, ProjectileID.ProximityMineII,
            ProjectileID.GrenadeIV, ProjectileID.RocketIV, ProjectileID.ProximityMineIV,
            ProjectileID.RocketSnowmanII, ProjectileID.RocketSnowmanIV,
            ProjectileID.Celeb2RocketExplosive, ProjectileID.Celeb2RocketExplosiveLarge,
            ProjectileID.ClusterRocketII, ProjectileID.ClusterGrenadeII, ProjectileID.ClusterMineII,
            ProjectileID.ClusterFragmentsII, ProjectileID.ClusterSnowmanRocketII, ProjectileID.ClusterSnowmanFragmentsII,
            ProjectileID.MiniNukeRocketII, ProjectileID.MiniNukeGrenadeII, ProjectileID.MiniNukeMineII,
            ProjectileID.MiniNukeSnowmanRocketII,
            ProjectileID.WetRocket, ProjectileID.WetGrenade, ProjectileID.WetMine, ProjectileID.WetSnowmanRocket,
            ProjectileID.LavaRocket, ProjectileID.LavaGrenade, ProjectileID.LavaMine, ProjectileID.LavaSnowmanRocket,
            ProjectileID.HoneyRocket, ProjectileID.HoneyGrenade, ProjectileID.HoneyMine, ProjectileID.HoneySnowmanRocket,
            ProjectileID.DryRocket, ProjectileID.DryGrenade, ProjectileID.DryMine, ProjectileID.DrySnowmanRocket
        };

        // 后续遇到需禁用的模组弹幕，在此按 ModName/ProjectileName 填写，不依赖运行时数字 ID。
        private static readonly string[] excludedModProjectileNames =
        {
            "TestMod/PhantasmHoldout", // 手持弓会再次选择/消耗弹药。
            "TestMod/SwordQiProjectile" // 依赖真实玩家到目标的轴线和额外 localAI 叠层。
        };
        private static readonly HashSet<int> excludedModTypes = new();

        public override void PostSetupContent()
        {
            ClearAttackFilters();
            foreach (string name in excludedModProjectileNames)
                if (ModContent.TryFind(name, out ModProjectile projectile)) excludedModTypes.Add(projectile.Type);
        }
        private static void ClearAttackFilters() => excludedModTypes.Clear();

        internal static bool CanCopyProjectile(Projectile p)
            => p.friendly && !p.hostile && !IsExcludedProjectile(p);

        internal static bool IsExcludedProjectile(Projectile p)
        {
            // 近战载体是本功能自己的特殊适配，不属于外部弹幕默认放行范围。
            if (p.ModProjectile is TimeEchoMeleeProjectile) return false;
            int type = p.type;
            if (type <= 0 || type >= ProjectileLoader.ProjectileCount || p.minion || p.sentry || p.minionSlots > 0 ||
                (p.CountsAsClass(DamageClass.Summon) && !ProjectileID.Sets.IsAWhip[type]) || p.usesIDStaticNPCImmunity ||
                Main.projPet[type] || ProjectileID.Sets.LightPet[type] ||
                ProjectileID.Sets.MinionSacrificable[type] || ProjectileID.Sets.MinionShot[type] ||
                ProjectileID.Sets.SentryShot[type] ||
                ProjectileID.Sets.IsADD2Turret[type] ||
                (p.aiStyle == ProjAIStyleID.HeldProjectile && !IsDirectHeldAttack(p)) ||
                excludedStyles.Contains(p.aiStyle) || excludedVanillaTypes.Contains(type) || excludedModTypes.Contains(type)) return true;
            // AIType 借用特殊原版 AI 时也过滤，不能仅凭模组弹幕自己的 type 放行。
            if (p.ModProjectile is { AIType: > 0 } mod && excludedVanillaTypes.Contains(mod.AIType)) return true;
            return !ProjectileID.Sets.IsAWhip[type] && !UsesEchoOwner(p) && p.owner >= 0 && p.owner < Main.maxPlayers &&
                Main.player[p.owner].heldProj == p.whoAmI;
        }

        internal static bool CanCopyChild(Projectile parent, Projectile child, IEntitySource source)
            // 命中派生可能使用已结算伤害或来自装备；未适配时不能默认继承并再次减半。
            => source is not IEntitySource_OnHit && child.owner == parent.owner && CanCopyProjectile(child);
    }
}
