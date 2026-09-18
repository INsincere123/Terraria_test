using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Dusts;

namespace TestMod.Common.Utilities
{
    public static class DustUtils
    {
        public static Dust SpawnCosmicSpark(Vector2 position, Vector2 velocity, Color color, float scale = 1f, int alpha = 0)
            => Spawn<CosmicSparkDust>(position, velocity, color, scale, alpha);

        public static Dust SpawnGraviticVoid(Vector2 position, Vector2 velocity, Color color, float scale = 1f, int alpha = 0)
            => Spawn<GraviticVoidDust>(position, velocity, color, scale, alpha);

        public static Dust SpawnTemporalShard(Vector2 position, Vector2 velocity, Color color, float scale = 1f, int alpha = 0)
            => Spawn<TemporalShardDust>(position, velocity, color, scale, alpha);

        public static Dust SpawnImpactLine(Vector2 position, Vector2 velocity, Color color, float scale = 1f, int alpha = 0)
            => Spawn<ImpactLineDust>(position, velocity, color, scale, alpha);

        public static Dust SpawnShieldShard(Vector2 position, Vector2 velocity, Color color, float scale = 1f, int alpha = 0)
            => Spawn<ShieldShardDust>(position, velocity, color, scale, alpha);

        public static Dust SpawnBloodMist(Vector2 position, Vector2 velocity, Color color, float scale = 1f, int alpha = 80)
            => Spawn<BloodMistDust>(position, velocity, color, scale, alpha);

        public static void SpawnCosmicBurst(Vector2 center, int count, Color color, float minSpeed, float maxSpeed, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(minSpeed, maxSpeed);
                SpawnCosmicSpark(center + velocity * 0.4f, velocity, color, scale * Main.rand.NextFloat(0.75f, 1.25f));
            }
        }

        public static void SpawnImpactBurst(Vector2 center, int count, Color color, float minSpeed, float maxSpeed, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(minSpeed, maxSpeed);
                SpawnImpactLine(center + velocity * 0.3f, velocity, color, scale * Main.rand.NextFloat(0.75f, 1.2f));
            }
        }

        public static void SpawnTemporalBurst(Vector2 center, int count, Color color, float minSpeed, float maxSpeed, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(minSpeed, maxSpeed);
                SpawnTemporalShard(center + velocity * 0.35f, velocity, color, scale * Main.rand.NextFloat(0.75f, 1.2f));
            }
        }

        public static void SpawnVoidBurst(Vector2 center, int count, Color color, float minSpeed, float maxSpeed, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(minSpeed, maxSpeed);
                SpawnGraviticVoid(center + velocity * 0.25f, velocity, color, scale * Main.rand.NextFloat(0.75f, 1.18f));
            }
        }

        public static void SpawnShieldBurst(Vector2 center, int count, Color color, float minSpeed, float maxSpeed, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(minSpeed, maxSpeed);
                SpawnShieldShard(center + velocity * 0.35f, velocity, color, scale * Main.rand.NextFloat(0.85f, 1.35f));
            }
        }

        public static void SpawnShieldHit(Player player, Color color, int hitDirection, int count = 12)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            float side = hitDirection == 0 ? (Main.rand.NextBool() ? 1f : -1f) : hitDirection;
            Vector2 outward = new Vector2(side, -0.12f).SafeNormalize(Vector2.UnitX);
            Vector2 center = player.Center + outward * player.width * 0.45f;

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = outward.RotatedByRandom(0.95f) * Main.rand.NextFloat(2.2f, 5.6f);
                velocity.Y += Main.rand.NextFloat(-1.8f, 0.8f);
                SpawnShieldShard(center + Main.rand.NextVector2Circular(8f, 12f), velocity, color, Main.rand.NextFloat(0.75f, 1.25f));
            }
        }

        public static void SpawnBloodMistCloud(Rectangle area, int count, Color color, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 position = new(
                    Main.rand.NextFloat(area.Left, area.Right),
                    Main.rand.NextFloat(area.Top, area.Bottom));
                Vector2 velocity = Main.rand.NextVector2Circular(1.8f, 1.1f) - Vector2.UnitY * Main.rand.NextFloat(0.2f, 1.1f);
                SpawnBloodMist(position, velocity, color, scale * Main.rand.NextFloat(0.75f, 1.35f));
            }
        }

        private static Dust Spawn<TDust>(Vector2 position, Vector2 velocity, Color color, float scale, int alpha)
            where TDust : ModDust
        {
            if (Main.netMode == NetmodeID.Server)
                return new Dust();

            Dust dust = Dust.NewDustPerfect(position, ModContent.DustType<TDust>(), velocity, alpha, color, scale);
            dust.noGravity = true;
            return dust;
        }
    }
}
