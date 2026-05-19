using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;

namespace TestMod.Projectiles.Minions
{
    public class AntaresMinion : ModProjectile
    {
        // ── 质变常量（槽位超过此值时触发全部质变效果） ──
        // ── 质变常量（槽位超过此值时触发全部质变效果） ──
        private const int AscensionThreshold = 11;      // 触发质变的槽位门槛
        private const float AscensionDamageMultiplier = 2.2f;       // 质变后额外伤害倍率（在原有 damageMod 基础上再乘）
        private const int AscensionThreshold2 = 21;     // 第二质变档位门槛
        private const float AscensionDamageMultiplier2 = 5f;        // 第二质变伤害倍率（直接替换第一档）

        private const int AscensionThreshold3 = 34;     // 第三质变档位门槛
        private const float AscensionDamageMultiplier3 = 66f;   // 第三质变伤害倍率
        private const float AntaresStarVisualScale = 0.68f;
        private const float StarParticleDensity = 0.8f;
        private const int MaxStarMotes = 64;

        public Player Owner => Main.player[Projectile.owner];
        public AntaresMinionPlayer ModdedOwner => Owner.GetModPlayer<AntaresMinionPlayer>();

        public ref float TimerForShooting => ref Projectile.ai[0];

        public int MinionSlotsToAdd
        {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        // localAI[0] 用作质变脉冲爆发计时器
        public ref float AscensionPulseTimer => ref Projectile.localAI[0];

        private bool spawnEffectPlayed;
        private readonly StarMote[] starMotes = new StarMote[MaxStarMotes];

        private readonly struct ConstellationStar
        {
            public readonly int UnlockSlot;
            public readonly Vector2 Offset;
            public readonly float Size;
            public readonly float Brightness;
            public readonly Color CoreColor;
            public readonly Color GlowColor;

            public ConstellationStar(int unlockSlot, Vector2 offset, float size, float brightness, Color coreColor, Color glowColor)
            {
                UnlockSlot = unlockSlot;
                Offset = offset;
                Size = size;
                Brightness = brightness;
                CoreColor = coreColor;
                GlowColor = glowColor;
            }
        }

        private readonly struct ConstellationLine
        {
            public readonly int UnlockSlot;
            public readonly int From;
            public readonly int To;
            public readonly float Weight;

            public ConstellationLine(int unlockSlot, int from, int to, float weight)
            {
                UnlockSlot = unlockSlot;
                From = from;
                To = to;
                Weight = weight;
            }
        }

        private struct StarMote
        {
            public bool Active;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Time;
            public float Lifetime;
            public float Scale;
            public float Rotation;
            public float AngularVelocity;
            public Color Color;
        }

        // The first 22 entries are projected from Scorpius' bright-star J2000 RA/Dec, centered on Antares.
        // Later entries are faint guide stars placed as paired legs around the body axis.
        private static readonly ConstellationStar[] Stars =
        [
            new(1,  new Vector2(0f, 0f),         1.85f, 1.25f, new Color(255, 105, 45), new Color(190, 25, 8)),    // Antares / alpha Sco
            new(20, new Vector2(-201f, 149f),    1.28f, 1.00f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Shaula / lambda Sco
            new(17, new Vector2(-213f, 232f),    1.18f, 0.93f, new Color(255, 238, 190), new Color(235, 170, 80)),  // Sargas / theta Sco
            new(4,  new Vector2(91f, -53f),      1.12f, 0.88f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Dschubba / delta Sco
            new(5,  new Vector2(-65f, 110f),     1.10f, 0.86f, new Color(255, 190, 120), new Color(220, 95, 35)),   // Larawag / epsilon Sco
            new(19, new Vector2(-229f, 176f),    1.06f, 0.84f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Girtab / kappa Sco
            new(6,  new Vector2(75f, -93f),      1.00f, 0.78f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Acrab / beta1 Sco
            new(21, new Vector2(-192f, 152f),    0.96f, 0.75f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Lesath / upsilon Sco
            new(3,  new Vector2(-20f, 25f),      0.92f, 0.72f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Paikauhale / tau Sco
            new(7,  new Vector2(96f, -5f),       0.90f, 0.70f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Fang / pi Sco
            new(2,  new Vector2(26f, -12f),      0.88f, 0.69f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Alniyat / sigma Sco
            new(18, new Vector2(-245f, 192f),    0.84f, 0.66f, new Color(255, 238, 190), new Color(235, 170, 80)),  // iota1 Sco
            new(9,  new Vector2(-70f, 163f),     0.82f, 0.64f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Xamidimura / mu1 Sco
            new(22, new Vector2(-252f, 149f),    0.78f, 0.61f, new Color(255, 205, 135), new Color(220, 120, 45)),  // G Sco
            new(16, new Vector2(-134f, 235f),    0.76f, 0.60f, new Color(255, 238, 190), new Color(235, 170, 80)),  // eta Sco
            new(13, new Vector2(-72f, 162f),     0.74f, 0.58f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Pipirima / mu2 Sco
            new(15, new Vector2(-79f, 223f),     0.72f, 0.56f, new Color(255, 175, 105), new Color(220, 90, 35)),   // zeta2 Sco
            new(10, new Vector2(102f, 39f),      0.68f, 0.53f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Iklil / rho Sco
            new(11, new Vector2(71f, -81f),      0.66f, 0.51f, new Color(190, 230, 255), new Color(70, 170, 255)),  // omega1 Sco
            new(14, new Vector2(55f, -98f),      0.64f, 0.50f, new Color(190, 230, 255), new Color(70, 170, 255)),  // Jabbah / nu Sco
            new(8,  new Vector2(0f, 70f),        0.54f, 0.42f, new Color(170, 220, 255), new Color(45, 135, 225)),  // upper right leg joint
            new(9,  new Vector2(48f, 58f),       0.50f, 0.38f, new Color(165, 215, 255), new Color(40, 125, 215)),  // upper right leg tip
            new(8,  new Vector2(-74f, 12f),      0.54f, 0.42f, new Color(170, 220, 255), new Color(45, 135, 225)),  // upper left leg joint
            new(9,  new Vector2(-88f, -28f),     0.50f, 0.38f, new Color(165, 215, 255), new Color(40, 125, 215)),  // upper left leg tip
            new(12, new Vector2(-56f, 198f),     0.50f, 0.39f, new Color(170, 220, 255), new Color(45, 135, 225)),  // lower right leg joint
            new(13, new Vector2(-6f, 192f),      0.46f, 0.35f, new Color(165, 215, 255), new Color(40, 125, 215)),  // lower right leg tip
            new(12, new Vector2(-128f, 146f),    0.50f, 0.39f, new Color(170, 220, 255), new Color(45, 135, 225)),  // lower left leg joint
            new(13, new Vector2(-142f, 96f),     0.46f, 0.35f, new Color(165, 215, 255), new Color(40, 125, 215)),  // lower left leg tip
        ];

        private static readonly ConstellationLine[] Lines =
        [
            new(2, 0, 10, 1.00f),
            new(3, 0, 8, 0.90f),
            new(4, 10, 3, 0.86f),
            new(5, 8, 4, 0.88f),
            new(6, 3, 6, 0.78f),
            new(7, 3, 9, 0.72f),
            new(8, 8, 20, 0.34f),
            new(8, 8, 22, 0.34f),
            new(9, 4, 12, 0.78f),
            new(9, 20, 21, 0.30f),
            new(9, 22, 23, 0.30f),
            new(10, 9, 17, 0.54f),
            new(11, 6, 18, 0.50f),
            new(12, 12, 24, 0.32f),
            new(12, 12, 26, 0.32f),
            new(13, 12, 15, 0.42f),
            new(13, 24, 25, 0.28f),
            new(13, 26, 27, 0.28f),
            new(14, 18, 19, 0.44f),
            new(15, 15, 16, 0.72f),
            new(16, 16, 14, 0.74f),
            new(17, 14, 2, 0.86f),
            new(18, 2, 11, 0.76f),
            new(19, 11, 5, 0.72f),
            new(20, 5, 1, 0.90f),
            new(21, 1, 7, 0.56f),
            new(22, 5, 13, 0.52f),
        ];

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            Main.projFrames[Type] = 1;
        }

        public override void SetDefaults()
        {
            Projectile.width = 38;                    // 宽度38像素
            Projectile.height = 48;                   // 高度48像素
            Projectile.minionSlots = 1f;              // 占用1个仆从槽位
            Projectile.penetrate = -1;                // 无穷穿透（仆从永不消失）
            Projectile.netImportant = true;           // 网络同步重要（多人模式同步）
            Projectile.friendly = true;               // 对玩家友好（不伤害玩家）
            Projectile.ignoreWater = true;            // 忽视水（不受水影响）
            Projectile.tileCollide = false;           // 穿过方块（不碰撞）
            Projectile.minion = true;                 // 标记为仆从
            Projectile.DamageType = DamageClass.Summon;  // 伤害类型：召唤伤害
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

            UpdateStarMotes();
            SpawnStarDust();

            // ── 质变脉冲爆发（每600帧一次，仅质变状态） ──
            if (Projectile.minionSlots > AscensionThreshold)
            {
                AscensionPulseTimer++;
                if (AscensionPulseTimer >= 600f)
                {
                    AscensionPulseTimer = 0f;
                    if (Main.netMode != NetmodeID.Server)
                    {
                        const int burstCount = 36;  // 爆发粒子数量，可调节
                        for (int d = 0; d < burstCount; d++)
                        {
                            float angle = MathHelper.TwoPi / burstCount * d;
                            Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 14f);
                            // RedTorch 和 Torch 交替，增加层次感
                            int dustType = (d % 2 == 0) ? DustID.RedTorch : DustID.Torch;
                            Dust dust = Dust.NewDustPerfect(Projectile.Center, dustType, vel,
                                100, default, Main.rand.NextFloat(1.2f, 2.0f));
                            dust.noGravity = true;
                        }
                    }
                }
            }
            else
            {
                AscensionPulseTimer = 0f;  // 退出质变时重置，避免重入时立刻爆发
            }
        }

        private void SpawnStarDust()
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            Vector2 center = Projectile.Center;
            int tier = GetAscensionTier();

            for (int i = 0; i < Stars.Length; i++)
            {
                ConstellationStar star = Stars[i];
                if (!IsStarUnlocked(star))
                    continue;

                bool isAntares = i == 0;
                bool shouldSpawn = Main.rand.NextBool(isAntares || tier > 0 ? 2 : 4);
                if (!shouldSpawn || Main.rand.NextFloat() > StarParticleDensity)
                    continue;

                Vector2 pos = center + GetStarOffset(star) * Projectile.scale;
                TrySpawnStarMote(pos, star, isAntares, tier);

                if (!Main.rand.NextBool(isAntares ? 5 : 8))
                    continue;

                int dustType = isAntares ? DustID.RedTorch : (tier >= 2 && Main.rand.NextBool(4) ? DustID.GoldFlame : DustID.BlueTorch);
                float intensity = star.Brightness * (isAntares ? 0.66f : 0.42f) * (1f + tier * 0.12f);
                int d = Dust.NewDust(pos - Vector2.One, 2, 2, dustType, 0f, 0f, 150, default, intensity);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.08f;
                Main.dust[d].scale = intensity * Main.rand.NextFloat(0.45f, 0.78f);
            }
        }

        private void UpdateStarMotes()
        {
            for (int i = 0; i < starMotes.Length; i++)
            {
                if (!starMotes[i].Active)
                    continue;

                starMotes[i].Time++;
                if (starMotes[i].Time >= starMotes[i].Lifetime)
                {
                    starMotes[i].Active = false;
                    continue;
                }

                starMotes[i].Position += starMotes[i].Velocity;
                starMotes[i].Velocity *= 0.982f;
                starMotes[i].Velocity = starMotes[i].Velocity.RotatedBy(starMotes[i].AngularVelocity * 0.018f);
                starMotes[i].Rotation += starMotes[i].AngularVelocity;
            }
        }

        private void TrySpawnStarMote(Vector2 position, ConstellationStar star, bool isAntares, int tier)
        {
            int index = -1;
            for (int i = 0; i < starMotes.Length; i++)
            {
                if (!starMotes[i].Active)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return;

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float speed = Main.rand.NextFloat(isAntares ? 0.10f : 0.06f, isAntares ? 0.42f : 0.28f) * (1f + tier * 0.08f);
            Vector2 velocity = angle.ToRotationVector2() * speed + new Vector2(-Projectile.spriteDirection * 0.035f, -0.018f);
            Color warm = Color.Lerp(new Color(255, 74, 18), new Color(255, 191, 88), Main.rand.NextFloat(0.45f));
            Color cool = Color.Lerp(star.CoreColor, new Color(160, 220, 255), Main.rand.NextFloat(0.35f));

            starMotes[index] = new StarMote
            {
                Active = true,
                Position = position + Main.rand.NextVector2Circular(3.2f, 3.2f),
                Velocity = velocity,
                Time = 0f,
                Lifetime = Main.rand.NextFloat(isAntares ? 34f : 26f, isAntares ? 58f : 44f) * (1f + tier * 0.08f),
                Scale = star.Brightness * Main.rand.NextFloat(isAntares ? 0.52f : 0.34f, isAntares ? 0.96f : 0.68f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                AngularVelocity = Main.rand.NextFloat(-0.025f, 0.025f),
                Color = isAntares ? warm : cool
            };
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
            Owner.AddBuff(ModContent.BuffType<AntaresBuff>(), 3600);
            if (Owner.dead)
                ModdedOwner.antares = false;
            if (ModdedOwner.antares)
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

            float timer = 90f * (4f / (4f + Projectile.minionSlots));   //槽位越多，分母越大，timer值越小 → 更频繁射击
            if (TimerForShooting < timer || Projectile.owner != Main.myPlayer)
                return;

            TimerForShooting = 0;

            SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.15f }, Projectile.Center);

            for (int d = 0; d < 16; d++)
            {
                float angle = MathHelper.TwoPi / 16 * d;
                Vector2 v = angle.ToRotationVector2() * 20f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PurificationPowder, v);
                dust.noGravity = true;
            }

            // 5% 概率触发爆发射击，数量 = 当前占用召唤槽
            int burstCount = Main.rand.NextFloat() < 0.05f ? (int)Projectile.minionSlots : 3;

            // 判断是否处于质变状态
            bool ascended = Projectile.minionSlots > AscensionThreshold;

            for (int i = 0; i < burstCount; i++)
            {
                if (burstCount > 3)
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f }, Projectile.Center); // 触发爆发时播放不同音效
                else
                    SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.15f }, Projectile.Center); // 原来的音效
                Vector2 velocity = new Vector2(25f, 0f).RotatedByRandom(MathHelper.Pi);
                float damageMod = 1f + MathF.Pow(0.06f * Projectile.minionSlots, 1.5f);

                // 根据档位应用质变伤害倍率
                if (Projectile.minionSlots > AscensionThreshold3)
                    damageMod *= AscensionDamageMultiplier3;
                else if (Projectile.minionSlots > AscensionThreshold2)
                    damageMod *= AscensionDamageMultiplier2;
                else if (ascended)
                    damageMod *= AscensionDamageMultiplier;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center + velocity,
                    velocity,
                    ModContent.ProjectileType<AntaresBeam>(),
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
            int tier = GetAscensionTier();
            float time = Main.GlobalTimeWrappedHourly;

            DrawUtils.PrepareForAdditivePrimitives(Main.spriteBatch);

            foreach (ConstellationLine line in Lines)
            {
                if (!IsLineUnlocked(line))
                    continue;

                Vector2 start = GetStarPosition(line.From, center);
                Vector2 end = GetStarPosition(line.To, center);
                DrawUtils.DrawConstellationLine(start, end, line.Weight, tier, time);
            }

            Main.spriteBatch.BeginSpriteBatch(SpriteSortMode.Deferred, SpriteBatchSettings.AdditiveLinearClamp);

            if (tier >= 2)
            {
                foreach (ConstellationLine line in Lines)
                {
                    if (!IsLineUnlocked(line))
                        continue;

                    Vector2 start = GetStarPosition(line.From, center);
                    Vector2 end = GetStarPosition(line.To, center);
                    DrawUtils.DrawEnergyPulse(Main.spriteBatch, start, end, line.Weight, tier, time + line.From * 0.19f);
                }
            }

            DrawStarMotes(Main.spriteBatch, time);

            for (int i = 0; i < Stars.Length; i++)
            {
                ConstellationStar star = Stars[i];
                if (!IsStarUnlocked(star))
                    continue;

                Vector2 pos = GetStarPosition(i, center);
                DrawConstellationStar(pos, star, i, tier, time);
            }

            Main.spriteBatch.RestartSpriteBatch(SpriteSortMode.Deferred, SpriteBatchSettings.AlphaBlendLinearClamp);

            return false;
        }

        private void DrawStarMotes(SpriteBatch spriteBatch, float time)
        {
            Texture2D texture = AntaresVisualAssetSystem.SoftStarMote;
            bool hasSoftTexture = AntaresVisualAssetSystem.HasSoftStarMote;
            Vector2 origin = hasSoftTexture ? texture.Size() * 0.5f : new Vector2(0.5f);

            for (int i = 0; i < starMotes.Length; i++)
            {
                if (!starMotes[i].Active)
                    continue;

                float completion = starMotes[i].Time / starMotes[i].Lifetime;
                float fadeIn = MathHelper.Clamp(completion / 0.18f, 0f, 1f);
                float fadeOut = 1f - MathHelper.Clamp((completion - 0.48f) / 0.52f, 0f, 1f);
                float shimmer = 0.82f + MathF.Sin(time * 5.1f + i * 1.73f) * 0.12f;
                float opacity = fadeIn * fadeOut * shimmer;
                float size = starMotes[i].Scale * (hasSoftTexture ? 0.14f : 2.2f) * (1f + completion * 0.34f);
                Vector2 drawPosition = starMotes[i].Position - Main.screenPosition;
                Color color = starMotes[i].Color * (0.44f * opacity);

                if (hasSoftTexture)
                {
                    spriteBatch.Draw(texture, drawPosition, null, color, starMotes[i].Rotation, origin, size, SpriteEffects.None, 0f);
                    spriteBatch.Draw(texture, drawPosition, null, Color.White * (0.16f * opacity), starMotes[i].Rotation, origin, size * 0.34f, SpriteEffects.None, 0f);
                }
                else
                {
                    spriteBatch.Draw(texture, drawPosition, null, color, starMotes[i].Rotation, origin, new Vector2(size), SpriteEffects.None, 0f);
                    spriteBatch.Draw(texture, drawPosition, null, Color.White * (0.13f * opacity), starMotes[i].Rotation, origin, new Vector2(size * 0.34f), SpriteEffects.None, 0f);
                }
            }
        }

        private int GetAscensionTier()
        {
            if (Projectile.minionSlots > AscensionThreshold3)
                return 3;
            if (Projectile.minionSlots > AscensionThreshold2)
                return 2;
            if (Projectile.minionSlots > AscensionThreshold)
                return 1;
            return 0;
        }

        private bool IsStarUnlocked(ConstellationStar star) => Projectile.minionSlots >= star.UnlockSlot;

        private bool IsLineUnlocked(ConstellationLine line) =>
            Projectile.minionSlots >= line.UnlockSlot &&
            (uint)line.From < Stars.Length &&
            (uint)line.To < Stars.Length;

        private Vector2 GetStarOffset(ConstellationStar star)
        {
            Vector2 offset = star.Offset;
            offset.X *= Projectile.spriteDirection;
            return offset;
        }

        private Vector2 GetStarPosition(int starIndex, Vector2 center) =>
            center + GetStarOffset(Stars[starIndex]) * Projectile.scale;

        private static void DrawConstellationStar(Vector2 worldPos, ConstellationStar star, int index, int tier, float time)
        {
            bool isAntares = index == 0;
            float visualScale = isAntares ? AntaresStarVisualScale : 1f;
            float shaderScale = star.Size * star.Brightness * visualScale;

            if (isAntares)
                DrawAntaresShaderStar(worldPos, shaderScale, tier, time);

            Color glow = Color.Lerp(star.GlowColor, new Color(255, 80, 22), isAntares ? MathHelper.Clamp(tier * 0.32f, 0f, 1f) : tier * 0.08f);
            Color core = Color.Lerp(star.CoreColor, Color.White, isAntares ? 0.15f + tier * 0.08f : 0.1f);
            Color spike = isAntares ? new Color(255, 140, 40) : new Color(170, 225, 255);

            DrawUtils.DrawPixelStar(
                Main.spriteBatch,
                worldPos,
                new PixelStarSettings(
                    core,
                    glow,
                    spike,
                    star.Size,
                    star.Brightness,
                    visualScale,
                    false,
                    false),
                tier,
                time,
                index);

            if (isAntares)
                DrawAntaresSoftStarMote(worldPos, star, tier, time);
        }

        private static void DrawAntaresSoftStarMote(Vector2 worldPos, ConstellationStar star, int tier, float time)
        {
            Texture2D texture = AntaresVisualAssetSystem.SoftStarMote;
            bool hasSoftTexture = AntaresVisualAssetSystem.HasSoftStarMote;
            Vector2 origin = hasSoftTexture ? texture.Size() * 0.5f : new Vector2(0.5f);
            Vector2 drawPosition = worldPos - Main.screenPosition;
            float twinkle = 0.92f + MathF.Sin(time * 3.1f) * 0.08f;
            float scale = star.Size * star.Brightness * AntaresStarVisualScale * (0.72f + tier * 0.045f) * twinkle;
            Color warmCore = Color.Lerp(new Color(255, 86, 24), new Color(255, 220, 92), 0.42f + tier * 0.08f);

            if (hasSoftTexture)
            {
                Main.spriteBatch.Draw(texture, drawPosition, null, new Color(255, 70, 18) * 0.58f, time * 0.28f, origin, scale * 0.18f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(texture, drawPosition, null, warmCore * 0.84f, -time * 0.18f, origin, scale * 0.1f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(texture, drawPosition, null, Color.White * 0.68f, 0f, origin, scale * 0.036f, SpriteEffects.None, 0f);
            }
            else
            {
                Main.spriteBatch.Draw(texture, drawPosition, null, warmCore * 0.88f, 0f, origin, new Vector2(scale * 2.8f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(texture, drawPosition, null, Color.White * 0.72f, 0f, origin, new Vector2(scale * 1.1f), SpriteEffects.None, 0f);
            }
        }

        private static bool DrawAntaresShaderStar(Vector2 worldPos, float scale, int tier, float time)
        {
            float sideLength = 82f * scale;
            float hasHaloNoise = AntaresVisualAssetSystem.HasHaloNoise ? 1f : 0f;
            float hasSpikeMask = AntaresVisualAssetSystem.HasSpikeMask ? 1f : 0f;
            float hasRadialRamp = AntaresVisualAssetSystem.HasRadialRamp ? 1f : 0f;

            return DrawUtils.TryDrawCenteredShaderQuad(
                "TestMod.AntaresStarShader",
                Terraria.GameContent.TextureAssets.MagicPixel.Value,
                worldPos,
                new Vector2(sideLength),
                shader =>
                {
                    shader.TrySetParameter("globalTime", time);
                    shader.TrySetParameter("starTier", (float)tier);
                    shader.TrySetParameter("starIntensity", 0.95f + tier * 0.08f);
                    shader.TrySetParameter("texturePresence", new Vector3(hasHaloNoise, hasSpikeMask, hasRadialRamp));
                    shader.SetTexture(AntaresVisualAssetSystem.HaloNoise, 2, SamplerState.LinearWrap);
                    shader.SetTexture(AntaresVisualAssetSystem.SpikeMask, 3, SamplerState.LinearClamp);
                    shader.SetTexture(AntaresVisualAssetSystem.RadialRamp, 4, SamplerState.LinearClamp);
                });
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
