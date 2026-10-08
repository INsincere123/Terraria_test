using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Projectiles.TimeEcho;
using EchoProjectile = TestMod.Common.GlobalProjectiles.GlobalProjectile;

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
        // 这里只描述已有的近战代理适配；是否复制本体由资源载体分流单独决定。
        internal static bool IsDirectHeldAttack(Projectile p) => p.aiStyle == ProjAIStyleID.HeldProjectile &&
            p.CountsAsClass(DamageClass.Melee);
        internal static bool UsesEchoOwner(Projectile p) => p.aiStyle == ProjAIStyleID.Boomerang ||
            p.aiStyle == ProjAIStyleID.Harpoon || p.aiStyle == ProjAIStyleID.Spear ||
            p.aiStyle == ProjAIStyleID.ShortSword || p.aiStyle == ProjAIStyleID.Zenith || IsDirectHeldAttack(p);

        private static readonly HashSet<int> excludedVanillaTypes = new()
        {
            // 工具及父载体绑定光束仍需特殊适配，不属于普通资源发射器。
            ProjectileID.LaserDrill, ProjectileID.LastPrism, ProjectileID.LastPrismLaser,
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
            "TestMod/SwordQiProjectile" // 依赖真实玩家到目标的轴线和额外 localAI 叠层。
        };
        private static readonly HashSet<int> excludedModTypes = new();

        public override void PostSetupContent()
        {
            ClearAttackFilters();
            foreach (string name in excludedModProjectileNames)
                if (ModContent.TryFind(name, out ModProjectile projectile)) excludedModTypes.Add(projectile.Type);
            LoadAttackAdapters();
        }
        private static void ClearAttackFilters() { excludedModTypes.Clear(); attackAdapters.Clear(); borrowedBallisticTypes.Clear(); shotCoordinates.Clear(); worldEffectTypes.Clear(); }

        internal static bool CanCopyProjectile(Projectile p)
            => p.friendly && !p.hostile && !IsExcludedProjectile(p);

        // 生成来源保存资源属性，避免换武器后用当前 HeldItem 误判旧弹幕。
        internal static bool IsResourceCarrier(Projectile p)
        {
            // 已适配的发射能力与资源无关；本体接触伤害另经几何入口处理。
            if (GetAttackAdapter(p) is { } adapter) return adapter.Emitter;
            EchoProjectile data = p.GetGlobalProjectile<EchoProjectile>();
            return data.EchoSourceUsesResources && (data.EchoResourceCarrier ||
                p.aiStyle == ProjAIStyleID.HeldProjectile ||
                (p.owner >= 0 && p.owner < Main.maxPlayers && Main.player[p.owner].heldProj == p.whoAmI));
        }

        internal static bool CanCopyProjectileBody(Projectile p) => CanCopyProjectile(p) &&
            GetAttackAdapter(p) == null && !IsResourceCarrier(p);

        // 明确标记为 arrow 的独立弹药按 Shoot 完成后的参数投送，自定义 AI 不影响其箭类身份。
        // 其他未知手持弹幕可能到首次 AI 才设置 heldProj，仍保留 AI 前快照再确认；已有载体先分流。
        internal static bool NeedsFirstAIConfirmation(Projectile p) => p.ModProjectile != null && GetAttackAdapter(p) == null &&
            p.GetGlobalProjectile<EchoProjectile>() is { EchoHasWeaponSource: true, EchoCompletedAI: false } &&
            !IsResourceCarrier(p) && !p.arrow && !(p.aiStyle == ProjAIStyleID.Arrow &&
                p.ModProjectile.AIType > 0 && p.ModProjectile.AIType < ProjectileID.Count && borrowedBallisticTypes.Contains(p.type));

        internal static bool IsExcludedProjectile(Projectile p)
        {
            if (worldEffectTypes.Contains(p.type) && p.ai[2] == 2) return true;
            // 近战载体是本功能自己的特殊适配，不属于外部弹幕默认放行范围。
            if (p.ModProjectile is TimeEchoMeleeProjectile) return false;
            int type = p.type;
            if (type <= 0 || type >= ProjectileLoader.ProjectileCount || p.minion || p.sentry || p.minionSlots > 0 ||
                (p.CountsAsClass(DamageClass.Summon) && !ProjectileID.Sets.IsAWhip[type]) ||
                Main.projPet[type] || ProjectileID.Sets.LightPet[type] ||
                ProjectileID.Sets.MinionSacrificable[type] || ProjectileID.Sets.MinionShot[type] ||
                ProjectileID.Sets.SentryShot[type] ||
                ProjectileID.Sets.IsADD2Turret[type] ||
                excludedStyles.Contains(p.aiStyle) ||
                (excludedVanillaTypes.Contains(type) && !IsEchoDamageFragment(p)) || excludedModTypes.Contains(type)) return true;
            // AIType 借用特殊原版 AI 时也过滤，不能仅凭模组弹幕自己的 type 放行。
            if (p.ModProjectile is { AIType: > 0 } mod &&
                excludedVanillaTypes.Contains(mod.AIType)) return true;
            return false;
        }

        internal static bool CanCopyChild(Projectile parent, Projectile child, IEntitySource source)
            // 命中派生可能使用已结算伤害或来自装备；未适配时不能默认继承并再次减半。
            => source is not IEntitySource_OnHit && child.owner == parent.owner && CanCopyProjectileBody(child);

        private static bool IsEchoDamageFragment(Projectile p)
            // 已进入副本链的原版集束碎片可保留伤害；炸地形由世界副作用钩子拦截。
            => p.GetGlobalProjectile<EchoProjectile>().IsTimeEchoAttack &&
                (p.type == ProjectileID.ClusterFragmentsII || p.type == ProjectileID.ClusterSnowmanFragmentsII);
    }
}
