using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Common.Utilities
{
    /// <summary>按本次伤害的载体分类，不从当前手持物品推断弹幕来源。</summary>
    internal static class MeleeHitClassifier
    {
        public static bool IsDirectMelee(Projectile projectile)
        {
            if (!projectile.DamageType.CountsAsClass(DamageClass.Melee)) return false;

            // 明确的本体/飞行类型优先；子弹幕不会自动继承父弹幕的快速资格。
            switch (projectile.type)
            {
                case ProjectileID.NightsEdge:
                case ProjectileID.Excalibur:
                case ProjectileID.TrueExcalibur:
                case ProjectileID.TerraBlade2:
                case ProjectileID.TheHorsemansBlade:
                case ProjectileID.Arkhalis:
                case ProjectileID.Terragrim:
                    return true;
                case ProjectileID.TrueNightsEdge:
                case ProjectileID.TerraBlade2Shot:
                    return false;
            }

            // 特定模组的本体/衍生弹幕规则优先于伤害类型标记。
            if (CalamityCompatSystem.TryGetMeleeProjectileOverride(projectile, out bool direct))
                return direct;

            // 灾厄标记只提供肯定证据，没有标记时继续检查原版规则。
            if (CalamityCompatSystem.IsTrueMeleeProj(projectile)) return true;

            // 自定义弹幕可能复用原版 AI，也可能在不同阶段飞出；未知类型保守降效。
            // 后续模组兼容应按具体弹幕类型/阶段登记，不能只看 heldProj 或距离。
            if (projectile.ModProjectile != null) return false;

            // 保留原有原版矛、短剑、链球等本体待遇；不要求它是 item.shoot。
            return projectile.aiStyle == ProjAIStyleID.Flail
                || projectile.aiStyle == ProjAIStyleID.Spear
                || projectile.aiStyle == ProjAIStyleID.ForwardStab
                || projectile.aiStyle == ProjAIStyleID.ShortSword
                || projectile.type == ProjectileID.SuperStarSlash
                || projectile.type == ProjectileID.BladeOfGrass
                || projectile.type == ProjectileID.Muramasa
                || projectile.type == ProjectileID.LightsBane;
        }
    }
}
