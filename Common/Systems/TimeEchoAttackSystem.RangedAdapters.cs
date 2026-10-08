using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DataStructures;

namespace TestMod.Common.Systems
{
    public sealed partial class TimeEchoAttackSystem
    {
        private const string RangedProjectiles = "CalamityMod.Projectiles.Ranged.";
        private const string RangedItems = "CalamityMod.Items.Weapons.Ranged.";
        private static readonly HashSet<int> worldEffectTypes = new();

        private static void RegisterRangedPolicies(ModProjectile template)
        {
            switch (template.GetType().FullName)
            {
                case RangedProjectiles + "UniversalGenesisStar":
                case RangedProjectiles + "PlanetaryAnnihilationProj":
                    shotCoordinates[template.Type] = TimeEchoShotCoordinates.WorldTarget;
                    break;
                case RangedProjectiles + "ScorchedEarthRocket":
                case RangedProjectiles + "HiveNuke":
                    // 同一类型的 ai[2]==2 是纯地形/液体控制阶段，不能以零伤害作通用判据。
                    worldEffectTypes.Add(template.Type);
                    break;
            }
        }

        internal static TimeEchoShotCoordinates GetSpawnCoordinates(Projectile p, Item item, int ordinal)
        {
            if (ordinal >= 0)
                switch (item?.ModItem?.GetType().FullName)
                {
                    case RangedItems + "PlanetaryAnnihilation":
                    case RangedItems + "Vortexpopper": return TimeEchoShotCoordinates.WorldTarget;
                    case RangedItems + "ConferenceCall" when ordinal >= 4: return TimeEchoShotCoordinates.WorldTarget;
                }
            return GetShotCoordinates(p);
        }

        internal static bool IsVortexpopperBubble(Projectile p, Item item)
            => p.type == ProjectileID.Xenopopper && item?.ModItem?.GetType().FullName == RangedItems + "Vortexpopper";

        private static AttackAdapter CreateRangedAdapter(Type type)
        {
            switch (type.FullName)
            {
                case RangedProjectiles + "FreedomStarHoldout": return new() { Emitter = true };
                case RangedProjectiles + "AbyssalFire":
                    return new() { RequiresParent = true, Capture = p => RectangleFrame(p) with {
                        HasRectangle = false, HasLine = true, LineStart = p.Center,
                        LineEnd = p.Center + p.velocity * 3330, LineWidth = 0, DrawSprite = false } };
                case RangedProjectiles + "FreedomStarBeam":
                    return new() { RequiresParent = true, ClipBeam = true, Capture = p => RectangleFrame(p) with {
                        HasLine = true, LineEnd = p.Center + p.velocity * p.ai[0], LineWidth = 15 * p.scale, DrawSprite = false } };
                case RangedProjectiles + "MagnomalyAura":
                    return new() { RequiresParent = true, ParentRelative = true, Capture = p => CircleFrame(p, p.Center, 100) with { DrawSprite = false } };
                case RangedProjectiles + "ExoLightningBolt":
                    return new() { WorldTarget = true, Capture = LightningFrame };
                default: return null;
            }
        }

        private static TimeEchoProjectionFrame LightningFrame(Projectile p)
        {
            // 原碰撞遍历非零 oldPos，宽度沿轨迹变化；不能用首尾直线代替折线路径。
            var points = new List<Vector2>(p.oldPos.Length);
            foreach (Vector2 point in p.oldPos)
                if (point != Vector2.Zero) points.Add(point);
            if (points.Count > TimeEchoProjectionFrame.MaxSegments + 1)
                throw new NotSupportedException("Lightning trail exceeds replay segment limit");
            var segments = new TimeEchoLineSegment[points.Count > 2 ? points.Count - 1 : 0];
            for (int i = 0; i < segments.Length; i++)
            {
                float progress = i / (float)points.Count;
                float width = (progress < 0.5f ? progress * 2 : 2 - progress * 2) * p.scale * p.width * 0.8f;
                segments[i] = new(points[i], points[i + 1], width);
            }
            return RectangleFrame(p) with { HasRectangle = false, HasLine = false,
                Segments = segments, DrawSprite = false, Damaging = p.friendly && !p.hostile && p.damage > 0 &&
                    ProjectileLoader.CanDamage(p) != false && segments.Length > 0 };
        }
    }
}
