using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using 武器test.Buffs;
using 武器test.Common.Players;

namespace 武器test.Projectiles.Minions
{
    public class SiriusMinion : ModProjectile
    {
        public Player Owner => Main.player[Projectile.owner];
        public SiriusMinionPlayer ModdedOwner => Owner.GetModPlayer<SiriusMinionPlayer>();

        public ref float TimerForShooting => ref Projectile.ai[0];

        public int MinionSlotsToAdd
        {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        private bool spawnEffectPlayed;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults()
        {
            Projectile.width = 38;
            Projectile.height = 48;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override void AI()
        {
            NPC target = FindTarget(5000f);

            // 吸收二次召唤塞进来的召唤槽。
            if (MinionSlotsToAdd > 0)
            {
                float available = Owner.maxMinions;
                foreach (var p in Main.ActiveProjectiles)
                {
                    if (p.owner == Projectile.owner)
                        available -= p.minionSlots;
                }
                while (available >= 1 && MinionSlotsToAdd > 0)
                {
                    Projectile.minionSlots++;
                    available--;
                    MinionSlotsToAdd--;
                    Projectile.netUpdate = true;
                }
                MinionSlotsToAdd = 0;
            }

            CheckMinionExistence();
            SpawnEffect();
            ShootTarget(target);

            Lighting.AddLight(Projectile.Center, 0.5f, 0.5f, 1f);

            TimerForShooting++;

            Projectile.scale = MathHelper.Lerp(0.3f, 0.33f, (1 + MathF.Sin(Projectile.frameCounter * 0.01f)) * 0.5f);
            Projectile.frameCounter++;
            if (Projectile.frameCounter > 31415)
                Projectile.frameCounter = 0;

            Projectile.spriteDirection = Owner.direction;

            Projectile.Center = Owner.oldPosition + Owner.Size * 0.5f
                                - new Vector2(64 * Projectile.spriteDirection, 96f - Owner.gfxOffY);
            Projectile.velocity = Vector2.Zero;

            SpawnStarDust();
        }

        private void SpawnStarDust()
        {
            Vector2 center = Projectile.Center;

            void Star(float slotReq, Vector2 offset, float intensity)
            {
                if (slotReq > 0 && Projectile.minionSlots < slotReq)
                    return;
                offset.X *= Projectile.spriteDirection;
                Vector2 pos = center + offset * Projectile.scale;
                if (Main.rand.NextBool(2))
                {
                    int d = Dust.NewDust(pos - Vector2.One * 2f, 4, 4, DustID.BlueTorch, 0f, 0f, 100, default, intensity);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity *= 0.2f;
                    Main.dust[d].scale = intensity * Main.rand.NextFloat(1f, 1.4f);
                }
            }

            Star(0, new Vector2(0f, 0f), 1.5f);
            Star(2, new Vector2(-118f, 217f), 0.75f);
            Star(3, new Vector2(-67f, 272f), 0.75f);
            Star(4, new Vector2(119f, 32f), 0.75f);
            Star(5, new Vector2(-192f, 284f), 0.75f);
            Star(6, new Vector2(-62f, 11f), 0.5f);
            Star(7, new Vector2(-50f, -103f), 0.5f);
            Star(8, new Vector2(-101f, -23f), 0.5f);
            Star(9, new Vector2(46f, 59f), 0.5f);
            Star(10, new Vector2(-49f, 166f), 0.5f);
        }

        private NPC FindTarget(float range)
        {
            if (Owner.HasMinionAttackTargetNPC)
            {
                NPC forced = Main.npc[Owner.MinionAttackTargetNPC];
                if (forced.CanBeChasedBy() && Vector2.Distance(forced.Center, Projectile.Center) <= range)
                    return forced;
            }

            NPC result = null;
            float minDist = range;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy())
                    continue;
                float d = Vector2.Distance(npc.Center, Projectile.Center);
                if (d < minDist && Collision.CanHit(Projectile.Center, 0, 0, npc.Center, 0, 0))
                {
                    minDist = d;
                    result = npc;
                }
            }
            return result;
        }

        private void CheckMinionExistence()
        {
            Owner.AddBuff(ModContent.BuffType<SiriusBuff>(), 3600);
            if (Owner.dead)
                ModdedOwner.sirius = false;
            if (ModdedOwner.sirius)
                Projectile.timeLeft = 2;
        }

        private void SpawnEffect()
        {
            if (spawnEffectPlayed) return;

            const int dustAmt = 50;
            for (int d = 0; d < dustAmt; d++)
            {
                float angle = MathHelper.TwoPi / dustAmt * d;
                Vector2 v = angle.ToRotationVector2() * 20f;
                Dust dust = Dust.NewDustPerfect(Owner.Center - Vector2.UnitY * 60f, DustID.PurificationPowder, v);
                dust.noGravity = true;
            }
            spawnEffectPlayed = true;
        }

        private void ShootTarget(NPC target)
        {
            if (target == null) return;

            float timer = 90f * (10f / (10f + Projectile.minionSlots));
            if (TimerForShooting < timer || Projectile.owner != Main.myPlayer)
                return;

            TimerForShooting = 0;

            SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.15f }, Projectile.Center);

            for (int d = 0; d < 50; d++)
            {
                float angle = MathHelper.TwoPi / 50 * d;
                Vector2 v = angle.ToRotationVector2() * 20f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PurificationPowder, v);
                dust.noGravity = true;
            }

            for (int i = 0; i < 2; i++)
            {
                Vector2 velocity = new Vector2(25f, 0f).RotatedByRandom(MathHelper.Pi);
                float damageMod = 1f + MathF.Pow(0.2f * Projectile.minionSlots, 1.5f);
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center + velocity,
                    velocity,
                    ModContent.ProjectileType<SiriusBeam>(),
                    (int)(Projectile.damage * damageMod),
                    Projectile.knockBack,
                    Projectile.owner);
            }
        }

        public override bool? CanDamage() => false;

        public override Color? GetAlpha(Color lightColor) => new Color(200, 200, 200, 200);

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 center = Projectile.Center;

            void Connect(float slotReq, Vector2 p1, Vector2 p2)
            {
                if (slotReq > 0 && Projectile.minionSlots < slotReq)
                    return;
                p1.X *= Projectile.spriteDirection;
                p2.X *= Projectile.spriteDirection;
                Color color = Color.SkyBlue * 0.75f * ((MathF.Sin(Main.GlobalTimeWrappedHourly) + 1f) * 0.25f + 0.5f);
                DrawLine(Main.spriteBatch,
                    center + p1 * Projectile.scale,
                    center + p2 * Projectile.scale,
                    color, 2f);
            }

            Connect(4, new Vector2(0f, 0f), new Vector2(119f, 32f));
            Connect(2, new Vector2(0f, 0f), new Vector2(-118f, 217f));
            Connect(6, new Vector2(0f, 0f), new Vector2(-62f, 11f));
            Connect(9, new Vector2(119f, 32f), new Vector2(46f, 59f));
            Connect(10, new Vector2(46f, 59f), new Vector2(-49f, 166f));
            Connect(10, new Vector2(-49f, 166f), new Vector2(-67f, 272f));
            Connect(3, new Vector2(-67f, 272f), new Vector2(-118f, 217f));
            Connect(5, new Vector2(-118f, 217f), new Vector2(-192f, 284f));
            Connect(8, new Vector2(-62f, 11f), new Vector2(-101f, -23f));
            Connect(8, new Vector2(-101f, -23f), new Vector2(-50f, -103f));
            Connect(7, new Vector2(-50f, -103f), new Vector2(-62f, 11f));

            return false;
        }

        private static void DrawLine(SpriteBatch sb, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 delta = end - start;
            float angle = delta.ToRotation();
            float length = delta.Length();
            sb.Draw(pixel,
                start - Main.screenPosition,
                new Rectangle(0, 0, 1, 1),
                color,
                angle,
                new Vector2(0f, 0.5f),
                new Vector2(length, thickness),
                SpriteEffects.None,
                0f);
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Projectile.minionSlots);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Projectile.minionSlots = reader.ReadSingle();
        }
    }
}