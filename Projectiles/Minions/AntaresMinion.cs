using System;
using System.IO;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Buffs;
using TestMod.Common.Players;

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

        private static readonly ConstellationStar[] Stars =
        [
            new(0,  new Vector2(0f, 0f),       1.85f, 1.25f, new Color(255, 105, 45), new Color(190, 25, 8)),    // Antares
            new(2,  new Vector2(70f, -80f),    1.05f, 0.82f, new Color(190, 230, 255), new Color(70, 170, 255)),  // delta Sco
            new(3,  new Vector2(-30f, 80f),    1.00f, 0.78f, new Color(190, 235, 255), new Color(60, 165, 255)),  // tau Sco
            new(4,  new Vector2(-80f, 150f),   0.95f, 0.72f, new Color(175, 225, 255), new Color(55, 150, 240)),  // epsilon Sco
            new(5,  new Vector2(140f, -140f),  1.16f, 0.88f, new Color(210, 240, 255), new Color(85, 180, 255)),  // beta Sco
            new(6,  new Vector2(-120f, 210f),  0.78f, 0.58f, new Color(165, 220, 255), new Color(45, 135, 225)),  // mu Sco
            new(7,  new Vector2(-170f, 240f),  0.72f, 0.54f, new Color(160, 215, 255), new Color(40, 125, 215)),  // zeta Sco
            new(8,  new Vector2(-210f, 200f),  0.72f, 0.56f, new Color(160, 215, 255), new Color(40, 130, 220)),  // theta Sco
            new(9,  new Vector2(-140f, 170f),  1.25f, 0.96f, new Color(230, 245, 255), new Color(110, 190, 255)), // lambda Sco
            new(10, new Vector2(190f, -100f),  0.82f, 0.60f, new Color(170, 220, 255), new Color(50, 140, 225)),  // rho Sco
            new(11, new Vector2(80f, -160f),   0.86f, 0.64f, new Color(175, 225, 255), new Color(55, 145, 235)),  // pi Sco
        ];

        private static readonly ConstellationLine[] Lines =
        [
            new(2, 0, 1, 1.00f),
            new(3, 0, 2, 1.00f),
            new(4, 2, 3, 0.95f),
            new(5, 1, 4, 0.92f),
            new(6, 3, 5, 0.85f),
            new(7, 5, 6, 0.78f),
            new(8, 6, 7, 0.76f),
            new(9, 7, 8, 1.00f),
            new(10, 4, 9, 0.72f),
            new(11, 4, 10, 0.72f),
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
            Vector2 center = Projectile.Center;
            int tier = GetAscensionTier();

            for (int i = 0; i < Stars.Length; i++)
            {
                ConstellationStar star = Stars[i];
                if (!IsStarUnlocked(star))
                    continue;

                bool isAntares = i == 0;
                bool shouldSpawn = Main.rand.NextBool(isAntares || tier > 0 ? 2 : 4);
                if (!shouldSpawn)
                    continue;

                Vector2 pos = center + GetStarOffset(star) * Projectile.scale;
                int dustType = isAntares ? DustID.RedTorch : (tier >= 2 && Main.rand.NextBool(4) ? DustID.GoldFlame : DustID.BlueTorch);
                float intensity = star.Brightness * (isAntares ? 1.35f : 0.85f) * (1f + tier * 0.18f);
                int d = Dust.NewDust(pos - Vector2.One * 2f, 4, 4, dustType, 0f, 0f, 100, default, intensity);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.2f;
                Main.dust[d].scale = intensity * Main.rand.NextFloat(0.9f, 1.35f);
            }
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

            for (int d = 0; d < 50; d++)
            {
                float angle = MathHelper.TwoPi / 50 * d;
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

            Main.spriteBatch.End();
            Main.instance.GraphicsDevice.BlendState = BlendState.Additive;

            foreach (ConstellationLine line in Lines)
            {
                if (!IsLineUnlocked(line))
                    continue;

                Vector2 start = GetStarPosition(line.From, center);
                Vector2 end = GetStarPosition(line.To, center);
                DrawConstellationLine(start, end, line.Weight, tier, time);
            }

            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix);

            if (tier >= 2)
            {
                foreach (ConstellationLine line in Lines)
                {
                    if (!IsLineUnlocked(line))
                        continue;

                    Vector2 start = GetStarPosition(line.From, center);
                    Vector2 end = GetStarPosition(line.To, center);
                    DrawEnergyPulse(Main.spriteBatch, start, end, line.Weight, tier, time + line.From * 0.19f);
                }
            }

            for (int i = 0; i < Stars.Length; i++)
            {
                ConstellationStar star = Stars[i];
                if (!IsStarUnlocked(star))
                    continue;

                Vector2 pos = GetStarPosition(i, center);
                DrawConstellationStar(Main.spriteBatch, pos, star, i, tier, time);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix);

            return false;
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

        private bool IsLineUnlocked(ConstellationLine line) => Projectile.minionSlots >= line.UnlockSlot;

        private Vector2 GetStarOffset(ConstellationStar star)
        {
            Vector2 offset = star.Offset;
            offset.X *= Projectile.spriteDirection;
            return offset;
        }

        private Vector2 GetStarPosition(int starIndex, Vector2 center) =>
            center + GetStarOffset(Stars[starIndex]) * Projectile.scale;

        private static void DrawConstellationLine(Vector2 start, Vector2 end, float weight, int tier, float time)
        {
            float shimmer = 0.72f + MathF.Sin(time * (1.15f + tier * 0.2f) + start.X * 0.01f) * 0.16f;
            float tierGlow = 1f + tier * 0.3f;
            Color outer = Color.Lerp(new Color(45, 120, 255), new Color(255, 70, 22), tier / 3f);
            Color inner = Color.Lerp(new Color(180, 235, 255), new Color(255, 205, 95), tier / 3f);
            Vector2[] points = CreateConstellationLinePoints(start, end, weight, tier, time);

            RenderPrimitiveLine(points, outer * (0.17f * shimmer), (3.8f + tier * 1.15f) * weight * tierGlow);
            RenderPrimitiveLine(points, outer * (0.28f * shimmer), (1.9f + tier * 0.45f) * weight);
            RenderPrimitiveLine(points, inner * (0.58f * shimmer), (0.72f + tier * 0.11f) * weight);
        }

        private static Vector2[] CreateConstellationLinePoints(Vector2 start, Vector2 end, float weight, int tier, float time)
        {
            Vector2 delta = end - start;
            Vector2 normal = new Vector2(-delta.Y, delta.X).SafeNormalize(Vector2.Zero);
            float waveAmplitude = (0.65f + tier * 0.28f) * weight;
            Vector2[] points = new Vector2[6];

            for (int i = 0; i < points.Length; i++)
            {
                float completion = i / (float)(points.Length - 1);
                float wave = MathF.Sin((completion * MathHelper.Pi + time * 0.55f) + start.X * 0.006f) * waveAmplitude;
                points[i] = Vector2.Lerp(start, end, completion) + normal * wave;
            }

            return points;
        }

        private static void RenderPrimitiveLine(Vector2[] points, Color color, float width)
        {
            PrimitiveRenderer.RenderTrail(
                points,
                new PrimitiveSettings(
                    _ => width,
                    completion =>
                    {
                        float fade = MathF.Sin(completion * MathHelper.Pi);
                        return color * MathHelper.Clamp(fade * 1.18f, 0f, 1f);
                    },
                    Smoothen: true),
                14);
        }

        private static void DrawEnergyPulse(SpriteBatch sb, Vector2 start, Vector2 end, float weight, int tier, float time)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);
            Vector2 point = Vector2.Lerp(start, end, time * (0.18f + tier * 0.03f) % 1f) - Main.screenPosition;
            float pulse = 1f + MathF.Sin(time * 6f) * 0.18f;
            Color color = tier >= 3 ? new Color(255, 190, 70) : new Color(140, 220, 255);

            sb.Draw(pixel, point, src, color * 0.35f, 0f, new Vector2(0.5f), 11f * weight * pulse, SpriteEffects.None, 0f);
            sb.Draw(pixel, point, src, Color.White * 0.78f, 0f, new Vector2(0.5f), 3.2f * weight * pulse, SpriteEffects.None, 0f);
        }

        private static void DrawConstellationStar(SpriteBatch sb, Vector2 worldPos, ConstellationStar star, int index, int tier, float time)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);
            Vector2 pos = worldPos - Main.screenPosition;
            bool isAntares = index == 0;
            bool isStinger = index == 8;
            float twinkle = 0.88f + MathF.Sin(time * (1.8f + index * 0.11f) + index * 1.37f) * 0.18f;
            float tierScale = 1f + tier * 0.18f;
            float scale = star.Size * star.Brightness * twinkle * tierScale * (isAntares ? AntaresStarVisualScale : 1f);

            if (isAntares && DrawAntaresShaderStar(worldPos, scale, tier, time))
                return;

            Color glow = Color.Lerp(star.GlowColor, new Color(255, 80, 22), isAntares ? MathHelper.Clamp(tier * 0.32f, 0f, 1f) : tier * 0.08f);
            Color core = Color.Lerp(star.CoreColor, Color.White, isAntares ? 0.15f + tier * 0.08f : 0.1f);

            sb.Draw(pixel, pos, src, glow * 0.16f, 0f, new Vector2(0.5f), 20f * scale, SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, src, glow * 0.28f, 0f, new Vector2(0.5f), 11f * scale, SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, src, core * 0.85f, 0f, new Vector2(0.5f), 4.2f * scale, SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, src, Color.White * 0.9f, 0f, new Vector2(0.5f), 1.55f * scale, SpriteEffects.None, 0f);

            if (isAntares || (tier >= 3 && isStinger))
                DrawStarSpikes(sb, pos, isAntares ? new Color(255, 140, 40) : new Color(170, 225, 255), scale, tier, time, isAntares);
        }

        private static bool DrawAntaresShaderStar(Vector2 worldPos, float scale, int tier, float time)
        {
            if (!ShaderManager.TryGetShader("TestMod.AntaresStarShader", out ManagedShader shader))
                return false;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float sideLength = 82f * scale;
            Vector2 quadAnchor = worldPos - new Vector2(sideLength * 0.5f, -sideLength * 0.5f);

            shader.TrySetParameter("globalTime", time);
            shader.TrySetParameter("starTier", (float)tier);
            shader.TrySetParameter("starIntensity", 0.95f + tier * 0.08f);
            PrimitiveRenderer.RenderQuad(
                pixel,
                quadAnchor,
                new Vector2(sideLength),
                0f,
                Color.White,
                shader);

            return true;
        }

        private static void DrawStarSpikes(SpriteBatch sb, Vector2 pos, Color color, float scale, int tier, float time, bool longSpikes)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle src = new Rectangle(0, 0, 1, 1);
            float rot = time * (longSpikes ? 0.4f : -0.25f);
            float length = (longSpikes ? 28f : 17f) * (1f + tier * 0.18f) * scale;
            float width = (longSpikes ? 1.7f : 1.1f) * scale;

            for (int i = 0; i < 4; i++)
            {
                float angle = rot + MathHelper.PiOver2 * i;
                DrawSpike(sb, pixel, src, pos, angle, length, width, color, 0.42f);
                DrawSpike(sb, pixel, src, pos, angle + MathHelper.PiOver4, length * 0.55f, width * 0.75f, color, 0.24f);
            }
        }

        private static void DrawSpike(SpriteBatch sb, Texture2D pixel, Rectangle src, Vector2 pos, float angle, float length, float width, Color color, float alpha)
        {
            sb.Draw(pixel, pos, src, color * alpha, angle, new Vector2(0f, 0.5f),
                new Vector2(length, width), SpriteEffects.None, 0f);
            sb.Draw(pixel, pos, src, color * alpha, angle + MathHelper.Pi, new Vector2(0f, 0.5f),
                new Vector2(length, width), SpriteEffects.None, 0f);
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
