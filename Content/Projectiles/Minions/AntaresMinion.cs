using System;
using System.IO;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;

namespace TestMod.Content.Projectiles.Minions
{
    public class AntaresMinion : ModProjectile
    {
        // ── 质变常量（槽位超过此值时触发全部质变效果） ──
        private const int AscensionThreshold = 11;      // 触发质变的槽位门槛
        private const float AscensionDamageMultiplier = 2.2f;       // 质变后额外伤害倍率（在原有 damageMod 基础上再乘）
        private const int AscensionThreshold2 = 21;     // 第二质变档位门槛
        private const float AscensionDamageMultiplier2 = 5f;        // 第二质变伤害倍率（直接替换第一档）

        private const int AscensionThreshold3 = 34;     // 第三质变档位门槛
        private const float AscensionDamageMultiplier3 = 66f;   // 第三质变伤害倍率
        private const int RevealTicks = 18;
        private const float ConstellationTilt = MathHelper.Pi / 9f; // 整体倾斜20度，再按玩家朝向镜像。
        private const int MaxStarMotes = 12;
        private const int MaxTransientSparks = 24;
        private const int MaxImpactFlashes = 4;
        private const int ImpactVisualTicks = 8;
        private const int ShotVisualTicks = 12;
        private const int TierTransitionTicks = 30;

        public Player Owner => Main.player[Projectile.owner];
        public AntaresMinionPlayer ModdedOwner => Owner.GetModPlayer<AntaresMinionPlayer>();

        public ref float TimerForShooting => ref Projectile.ai[0];

        public int MinionSlotsToAdd
        {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        // 保留原有计时访问入口，客户端始终清零，不再驱动周期效果。
        public ref float AscensionPulseTimer => ref Projectile.localAI[0];

        // 新增视觉状态按实体保存，不增加网络数据。
        private int visualAge;
        private int shotAge = ShotVisualTicks;
        private int burstAge = ShotVisualTicks;
        private float visualTier;
        private float tierFrom;
        private int tierTarget;
        private int tierAge = TierTransitionTicks;
        private ulong lastSparkTick;
        private bool hasSpark;
        private bool pendingSpark;
        private bool pendingBurstSpark;
        private ulong lastImpactTick;
        private bool hasImpact;
        private int lastImpactIndex = -1;
        private ulong lastShotVisualTick;
        private bool hasShotVisual;
        private bool visualInitialized;
        private bool remoteSnapshot;
        private float[] starReveal;
        private Terraria.Utilities.UnifiedRandom visualRandom;
        private ProjectileTargetCache _targetCache;
        private StarMote[] starMotes;
        private StarMote[] transientSparks;
        private ImpactFlash[] impactFlashes;

        private struct ImpactFlash
        {
            public bool Active;
            public Vector2 Position;
            public float Rotation;
            public int Age;
            public bool Burst;
        }

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
            public readonly bool Bent;

            public ConstellationLine(int unlockSlot, int from, int to, float weight, bool bent = false)
            {
                UnlockSlot = unlockSlot;
                From = from;
                To = to;
                Weight = weight;
                Bent = bent;
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

        // 保留主干、开口双钳和折足；省去钳掌、腹节与步足关节上的重复星点。
        // 折足只在末端布星，弯折由连线表现；10槽形成完整24点轮廓，后续槽位只改变恒星活动。
        private static readonly ConstellationStar[] Stars =
        [
            // 主干/尾部按用户提供的星座示意图取关键转折，统一缩放后再整体倾斜20度。
            new(1, Vector2.Zero, 1.85f, 1.25f, new Color(255, 105, 45), new Color(190, 25, 8)),
            CoolStar(2, -51, 93, 0.90f), // 躯干：从心宿二斜向下延伸
            CoolStar(3, -53, 137, 0.85f), // 躯干：转为近竖直
            CoolStar(4, -55, 188, 0.88f), // 尾根：躯干底端
            CoolStar(5, -146, 205, 0.92f), // 尾底：横向展开至 Sargas
            CoolStar(6, -182, 180, 0.96f), // 尾弯：底部外侧转角
            CoolStar(7, -185, 145, 1.00f), // 尾钩：短段向上
            CoolStar(7, -152, 137, 1.06f), // 尾针：向躯干方向回收
            CoolStar(8, -85, -40, 0.84f), // 左钳掌
            CoolStar(9, -116, -83, 0.90f), // 左钳外指
            CoolStar(9, -59, -80, 0.82f), // 左钳内指
            CoolStar(10, 85, -40, 0.84f), // 右钳掌
            CoolStar(10, 116, -83, 0.90f), // 右钳外指
            CoolStar(10, 59, -80, 0.82f), // 右钳内指
            CoolStar(3, -50, 36, 0.62f), // 左胸缘
            CoolStar(3, 10, 36, 0.62f), // 右胸缘
            CoolStar(4, -77, 93, 0.60f), // 左腹缘
            CoolStar(4, -25, 93, 0.60f), // 右腹缘
            CoolStar(8, -120, 71, 0.60f), // 第一对步足
            CoolStar(8, 80, 71, 0.60f),
            CoolStar(9, -118, 123, 0.60f), // 第二对步足
            CoolStar(9, 44, 123, 0.60f),
            CoolStar(10, -105, 163, 0.58f), // 第三对步足
            CoolStar(10, 42, 143, 0.58f),
        ];

        private static ConstellationStar CoolStar(int slot, float x, float y, float size) =>
            new(slot, new Vector2(x, y), size, 0.82f,
                new Color(190, 230, 255), new Color(55, 135, 225));

        private static readonly ConstellationLine[] Lines =
        [
            new(2, 0, 1, 1f), new(3, 1, 2, 1f), new(4, 2, 3, 1f),
            new(5, 3, 4, 1f), new(6, 4, 5, 1f), new(7, 5, 6, 1f), new(7, 6, 7, 1f),
            new(8, 0, 8, 0.90f), new(9, 8, 9, 0.95f), new(9, 8, 10, 0.95f),
            new(10, 0, 11, 0.90f), new(10, 11, 12, 0.95f), new(10, 11, 13, 0.95f),
            new(3, 0, 14, 0.70f), new(3, 0, 15, 0.70f),
            new(4, 14, 16, 0.70f), new(4, 15, 17, 0.70f),
            new(4, 16, 3, 0.70f), new(4, 17, 3, 0.70f),
            new(8, 14, 18, 0.65f, true), new(8, 15, 19, 0.65f, true),
            new(9, 16, 20, 0.65f, true), new(9, 17, 21, 0.65f, true),
            new(10, 16, 22, 0.60f, true), new(10, 17, 23, 0.60f, true),
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
            ShootTarget(target);

            if (!Main.dedServ)
                Lighting.AddLight(Projectile.Center, 0.65f, 0.24f, 0.12f);

            TimerForShooting++;

            Projectile.scale = MathHelper.Lerp(0.3f, 0.33f, (1 + MathF.Sin(Projectile.frameCounter * 0.01f)) * 0.5f);
            Projectile.frameCounter++;
            if (Projectile.frameCounter > 31415)
                Projectile.frameCounter = 0;

            Projectile.spriteDirection = Owner.direction;

            Projectile.Center = Owner.oldPosition + Owner.Size * 0.5f
                                - new Vector2(64 * Projectile.spriteDirection, 96f - Owner.gfxOffY);
            Projectile.velocity = Vector2.Zero;

            if (!Main.dedServ)
                UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (!visualInitialized)
            {
                starReveal = new float[Stars.Length];
                starMotes = new StarMote[MaxStarMotes];
                transientSparks = new StarMote[MaxTransientSparks];
                impactFlashes = new ImpactFlash[MaxImpactFlashes];
                visualInitialized = true;
                tierTarget = GetAscensionTier();
                visualTier = tierFrom = remoteSnapshot ? tierTarget : 0f;
                tierAge = remoteSnapshot ? TierTransitionTicks : 0;
                visualRandom = new Terraria.Utilities.UnifiedRandom(Projectile.identity * 397 ^ Projectile.owner);
                if (remoteSnapshot)
                {
                    visualAge = RevealTicks * 2;
                    for (int i = 0; i < Stars.Length; i++)
                        starReveal[i] = IsStarUnlocked(Stars[i]) ? 1f : 0f;
                }
            }

            visualAge++;
            shotAge = Math.Min(ShotVisualTicks, shotAge + 1);
            burstAge = Math.Min(ShotVisualTicks, burstAge + 1);
            int targetTier = GetAscensionTier();
            if (tierTarget != targetTier)
            {
                tierFrom = visualTier;
                tierTarget = targetTier;
                tierAge = 0;
            }
            tierAge = Math.Min(TierTransitionTicks, tierAge + 1);
            visualTier = MathHelper.SmoothStep(tierFrom, tierTarget, tierAge / (float)TierTransitionTicks);
            for (int i = 0; i < Stars.Length; i++)
            {
                // 首次召唤先亮主星，其他节点晚 18 tick 开始；后续新增槽位直接淡入。
                bool visible = IsStarUnlocked(Stars[i]) && (i == 0 || visualAge > RevealTicks);
                starReveal[i] = visible ? Math.Min(1f, starReveal[i] + 1f / RevealTicks) : 0f;
            }

            // 旧的600 tick周期爆发停用；保留原公开访问入口，不再驱动效果。
            AscensionPulseTimer = 0f;

            UpdateMotes(starMotes);
            UpdateMotes(transientSparks);
            for (int i = 0; i < impactFlashes.Length; i++)
            {
                if (impactFlashes[i].Active && ++impactFlashes[i].Age >= ImpactVisualTicks)
                    impactFlashes[i].Active = false;
            }
            SpawnShotSparks();
            SpawnStarDust();
        }

        private void SpawnStarDust()
        {
            if (visualAge % 6 != 0 || starReveal[0] < 1f)
                return;
            TrySpawnStarMote();
        }

        private static float ShotEnvelope(int age)
        {
            if (age <= 2 || age >= ShotVisualTicks)
                return 0f;
            return age < 4 ? (age - 2f) * 0.5f : (ShotVisualTicks - age) / 8f;
        }

        // 新光束是本地/远端共同的触发来源，同tick内爆发优先；亮度只刷新、不累加。
        internal void NotifyShotVisual(bool isBurst)
        {
            if (Main.dedServ || !visualInitialized)
                return;
            if (!hasShotVisual || lastShotVisualTick != Main.GameUpdateCount)
                shotAge = 0;
            if (isBurst)
                burstAge = 0;
            hasShotVisual = true;
            lastShotVisualTick = Main.GameUpdateCount;
            // 延迟到本体下一次更新，合并同tick的三束反馈，保证爆发配色优先。
            pendingSpark = true;
            pendingBurstSpark |= isBurst;
        }

        private void SpawnShotSparks()
        {
            if (!pendingSpark)
                return;
            bool isBurst = pendingBurstSpark;
            pendingSpark = pendingBurstSpark = false;
            if (hasSpark && Main.GameUpdateCount - lastSparkTick < 8)
                return;
            hasSpark = true;
            lastSparkTick = Main.GameUpdateCount;
            Color color = isBurst ? new Color(130, 205, 255) : new Color(255, 148, 55);
            for (int i = 0; i < 3; i++)
            {
                Vector2 velocity = visualRandom.NextVector2CircularEdge(1f, 1f) * visualRandom.NextFloat(0.8f, 1.6f);
                TrySpawnTransient(Projectile.Center, velocity, color, 12f, 0.7f);
            }
        }

        // 命中只写客户端视觉池，不生成弹幕或结算伤害。
        internal void NotifyImpactVisual(Vector2 position, Vector2 velocity, bool isBurst)
        {
            if (Main.dedServ || !visualInitialized)
                return;
            if (hasImpact && Main.GameUpdateCount - lastImpactTick < 6)
            {
                if (isBurst && Main.GameUpdateCount == lastImpactTick && lastImpactIndex >= 0)
                    impactFlashes[lastImpactIndex].Burst = true;
                return;
            }
            int index = Array.FindIndex(impactFlashes, flash => !flash.Active);
            if (index < 0)
                return;
            lastImpactTick = Main.GameUpdateCount;
            hasImpact = true;
            lastImpactIndex = index;
            impactFlashes[index] = new ImpactFlash
            {
                Active = true, Position = position, Rotation = velocity.ToRotation(), Burst = isBurst
            };
            Color color = isBurst ? new Color(105, 195, 255) : new Color(255, 150, 55);
            Vector2 direction = velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 4; i++)
                TrySpawnTransient(position, direction.RotatedBy(visualRandom.NextFloat(-1.1f, 1.1f)) *
                    visualRandom.NextFloat(0.7f, 2.1f), color, 10f, 0.65f);
        }

        private void TrySpawnTransient(Vector2 position, Vector2 velocity, Color color, float lifetime, float scale)
        {
            for (int i = 0; i < transientSparks.Length; i++)
            {
                if (transientSparks[i].Active)
                    continue;
                transientSparks[i] = new StarMote
                {
                    Active = true, Position = position, Velocity = velocity, Color = color,
                    Lifetime = lifetime, Scale = scale, Rotation = velocity.ToRotation()
                };
                return;
            }
        }

        private static void UpdateMotes(StarMote[] motes)
        {
            for (int i = 0; i < motes.Length; i++)
            {
                if (!motes[i].Active)
                    continue;
                if (++motes[i].Time >= motes[i].Lifetime)
                {
                    motes[i].Active = false;
                    continue;
                }
                motes[i].Position += motes[i].Velocity;
                motes[i].Velocity *= 0.94f;
                motes[i].Rotation += motes[i].AngularVelocity;
            }
        }

        private void TrySpawnStarMote()
        {
            for (int i = 0; i < starMotes.Length; i++)
            {
                if (starMotes[i].Active)
                    continue;
                Vector2 radial = visualRandom.NextVector2CircularEdge(1f, 1f);
                starMotes[i] = new StarMote
                {
                    Active = true,
                    Position = Projectile.Center + radial * visualRandom.NextFloat(3f, 5f),
                    Velocity = radial * visualRandom.NextFloat(0.08f, 0.18f),
                    Lifetime = visualRandom.NextFloat(38f, 62f),
                    Scale = visualRandom.NextFloat(0.45f, 0.85f),
                    Rotation = visualRandom.NextFloat(MathHelper.TwoPi),
                    AngularVelocity = visualRandom.NextFloat(-0.012f, 0.012f),
                    Color = Color.Lerp(new Color(220, 65, 18), new Color(255, 170, 65), visualRandom.NextFloat())
                };
                return;
            }
        }

        private NPC FindTarget(float range)
        {
            // 普通目标保持 CanHit 视线要求；右键指定目标沿用原有穿墙与包含边界的规则。
            int targetIndex = _targetCache.FindNearest(Projectile, Projectile.Center, range, out _,
                prioritizeMinionTarget: true, requireLineOfSight: true, includePreferredBoundary: true);
            return targetIndex >= 0 ? Main.npc[targetIndex] : null;
        }

        private void CheckMinionExistence()
        {
            Owner.AddBuff(ModContent.BuffType<AntaresBuff>(), 3600);
            if (Owner.dead)
                ModdedOwner.antares = false;
            if (ModdedOwner.antares)
                Projectile.timeLeft = 2;
        }

        private void ShootTarget(NPC target)
        {

            if (target == null) return;

            float timer = 90f * (4f / (4f + Projectile.minionSlots));   //槽位越多，分母越大，timer值越小 → 更频繁射击
            if (TimerForShooting < timer || Projectile.owner != Main.myPlayer)
                return;

            TimerForShooting = 0;

            // 每轮只抽取一次，爆发将原来的额外数量折算为三束光束的伤害。
            bool isBurst = Main.rand.NextFloat() < 0.05f;
            SoundEngine.PlaySound(isBurst
                ? SoundID.Item122 with { Pitch = 0.3f }
                : SoundID.Item9 with { Pitch = -0.15f }, Projectile.Center);
            int damage = CalculateShotDamage(Projectile.damage, Projectile.minionSlots, isBurst);
            for (int i = 0; i < 3; i++)
            {
                Vector2 velocity = new Vector2(25f, 0f).RotatedByRandom(MathHelper.Pi);
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center + velocity,
                    velocity,
                    ModContent.ProjectileType<AntaresBeam>(),
                    damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    ai2: isBurst ? 1f : 0f);
            }
        }

        private static int CalculateShotDamage(int baseDamage, float slots, bool isBurst)
        {
            // 先按原规则截断普通单束伤害，再应用爆发倍率。
            int normalDamage = (int)(baseDamage * GetShotDamageMultiplier(slots));
            return isBurst ? (int)(normalDamage * Math.Max(1f, slots / 3f)) : normalDamage;
        }

        internal int AscensionTier => GetAscensionTier();
        internal float SlotDamageMultiplier => GetShotDamageMultiplier(Projectile.minionSlots);

        private static float GetShotDamageMultiplier(float slots)
        {
            float multiplier = 1f + MathF.Pow(0.06f * slots, 1.5f);
            if (slots > AscensionThreshold3)
                multiplier *= AscensionDamageMultiplier3;
            else if (slots > AscensionThreshold2)
                multiplier *= AscensionDamageMultiplier2;
            else if (slots > AscensionThreshold)
                multiplier *= AscensionDamageMultiplier;
            return multiplier;
        }

        public override bool? CanDamage() => false;

        public override void OnKill(int timeLeft)
        {
            // 即使原版弹幕槽暂时保留实例，也不保留已取消召唤的客户端视觉池。
            starReveal = null;
            starMotes = null;
            transientSparks = null;
            impactFlashes = null;
            visualRandom = null;
            visualInitialized = false;
            pendingSpark = pendingBurstSpark = false;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(200, 200, 200, 200);

        public override bool PreDraw(ref Color lightColor)
        {
            if (Main.dedServ || !visualInitialized)
                return false;
            Vector2 center = Projectile.Center;
            float time = Main.GlobalTimeWrappedHourly;
            float mainOpacity = starReveal[0];
            float release = Math.Max(ShotEnvelope(shotAge), ShotEnvelope(burstAge));
            float burstRelease = ShotEnvelope(burstAge);
            bool spritesBegun = false;
            Main.spriteBatch.End();
            try
            {
                Main.spriteBatch.BeginSpriteBatch(SpriteSortMode.Deferred, SpriteBatchSettings.AlphaBlendLinearClamp);
                spritesBegun = true;
                DrawStarBackgrounds(center);
                Main.spriteBatch.End();
                spritesBegun = false;
                // 表面和日冕只承担底色，亮核留给最后一层；primitive在精灵批次外执行。
                Main.instance.GraphicsDevice.BlendState = BlendState.AlphaBlend;
                DrawAntaresShaderStar(center, time, mainOpacity, release);
                Main.spriteBatch.BeginSpriteBatch(SpriteSortMode.Deferred, SpriteBatchSettings.AdditiveLinearClamp);
                spritesBegun = true;
                DrawStarMotes(Main.spriteBatch, time);
                foreach (ConstellationLine line in Lines)
                {
                    if (IsLineUnlocked(line))
                        DrawLine(line, center, time);
                }
                for (int i = 0; i < Stars.Length; i++)
                {
                    if (starReveal[i] > 0f)
                        DrawStarSpikes(GetStarPosition(i, center), i, time, burstRelease);
                }
                for (int i = 0; i < Stars.Length; i++)
                {
                    if (starReveal[i] > 0f)
                        DrawStarCore(GetStarPosition(i, center), i, time, burstRelease);
                }
                DrawTransientFeedback();
            }
            finally
            {
                if (spritesBegun)
                    Main.spriteBatch.End();
                Main.spriteBatch.BeginSpriteBatch(SpriteSortMode.Deferred, SpriteBatchSettings.AlphaBlendLinearClamp);
            }
            return false;
        }

        private void DrawLine(ConstellationLine line, Vector2 center, float time)
        {
            Vector2 start = GetStarPosition(line.From, center);
            Vector2 end = GetStarPosition(line.To, center);
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length < 0.01f)
                return;
            float reveal = Math.Min(starReveal[line.From], starReveal[line.To]);
            float shimmer = 0.86f + MathF.Sin(time * 1.4f + line.To) * 0.08f;
            float opacity = reveal * shimmer;
            float weight = line.Weight;
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            float rotation = delta.ToRotation();
            if (line.Bent)
            {
                // 在未旋转的局部坐标中折足，关节处不再增加星点。
                Vector2 localStart = Stars[line.From].Offset;
                Vector2 localEnd = Stars[line.To].Offset;
                Vector2 bend = new Vector2(MathHelper.Lerp(localStart.X, localEnd.X, 0.62f),
                    MathHelper.Lerp(localStart.Y, localEnd.Y, 0.18f)).RotatedBy(ConstellationTilt);
                bend.X *= Projectile.spriteDirection;
                bend = center + bend * Projectile.scale;
                start += (bend - start).SafeNormalize(Vector2.Zero) * 1.8f;
                end -= (end - bend).SafeNormalize(Vector2.Zero) * 1.8f;
                DrawLineBand(start, bend, weight, opacity);
                DrawLineBand(bend, end, weight, opacity);
            }
            else
            {
                float startGap = line.From == 0 ? 3.6f : 1.8f;
                if (length <= startGap + 1.8f)
                    return;
                Vector2 direction = delta / length;
                start += direction * startGap;
                end -= direction * 1.8f;
                DrawLineBand(start, end, weight, opacity);
            }
            float energy = MathHelper.Clamp(visualTier - 1f, 0f, 1f);
            if (line.To > 7 || line.From != line.To - 1 || energy <= 0f)
                return;

            // 按实际路径长度采样宽而淡的亮度包络，跨节点连续，不绘制移动方点。
            const int samples = 8;
            float distance = BodyDistances[line.From];
            float segmentLength = BodyDistances[line.To] - distance;
            float fromFraction = (line.From == 0 ? 3.6f : 1.8f) / length;
            float toFraction = 1f - 1.8f / length;
            for (int i = 0; i < samples; i++)
            {
                float t0 = i / (float)samples, t1 = (i + 1f) / samples;
                float sample = MathHelper.Lerp(fromFraction, toFraction, (t0 + t1) * 0.5f);
                float envelope = BodyEnvelope(distance + segmentLength * sample, time);
                Color color = Color.Lerp(new Color(100, 185, 255), new Color(185, 215, 255), visualTier / 3f);
                Vector2 p0 = Vector2.Lerp(start, end, t0), p1 = Vector2.Lerp(start, end, t1);
                Main.spriteBatch.Draw(pixel, p0 - Main.screenPosition, source,
                    color * (opacity * envelope * 0.18f * energy), rotation,
                    new Vector2(0f, 0.5f), new Vector2(Vector2.Distance(p0, p1), 1.1f), SpriteEffects.None, 0f);
            }
        }

        private static readonly float[] BodyDistances = CreateBodyDistances();

        private static float[] CreateBodyDistances()
        {
            float[] distances = new float[8];
            for (int i = 1; i < distances.Length; i++)
                distances[i] = distances[i - 1] + Vector2.Distance(Stars[i - 1].Offset, Stars[i].Offset);
            return distances;
        }

        private static float BodyEnvelope(float distance, float time)
        {
            // 循环两端留出三倍包络宽度，让尾端淡出后再从主星端进入。
            float center = (time * 0.10f % 1f) * (BodyDistances[7] + 228f) - 114f;
            float x = (distance - center) / 38f;
            return MathF.Exp(-x * x);
        }

        private static void DrawLineBand(Vector2 start, Vector2 end, float weight, float opacity)
        {
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Rectangle source = new(0, 0, 1, 1);
            Vector2 delta = end - start;
            Vector2 position = start - Main.screenPosition;
            float length = delta.Length();
            float rotation = delta.ToRotation();
            Main.spriteBatch.Draw(pixel, position, source, new Color(55, 135, 225) * (0.08f * opacity * weight),
                rotation, new Vector2(0f, 0.5f), new Vector2(length, 1.8f * weight), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, position, source, new Color(130, 185, 235) * (0.46f * opacity * weight * weight),
                rotation, new Vector2(0f, 0.5f), new Vector2(length, Math.Max(0.75f, 1.05f * weight)), SpriteEffects.None, 0f);
        }

        private void DrawStarMotes(SpriteBatch spriteBatch, float time)
        {
            Texture2D texture = AntaresVisualAssetSystem.SoftStarMote;
            bool hasSoftTexture = AntaresVisualAssetSystem.HasSoftStarMote;
            Vector2 origin = hasSoftTexture ? texture.Size() * 0.5f : new Vector2(0.5f);
            Rectangle? source = hasSoftTexture ? null : new Rectangle(0, 0, 1, 1);
            Vector2 dimensions = hasSoftTexture ? texture.Size() : Vector2.One;

            for (int i = 0; i < starMotes.Length; i++)
            {
                if (!starMotes[i].Active)
                    continue;

                float completion = starMotes[i].Time / starMotes[i].Lifetime;
                float fadeIn = MathHelper.Clamp(completion / 0.18f, 0f, 1f);
                float fadeOut = 1f - MathHelper.Clamp((completion - 0.48f) / 0.52f, 0f, 1f);
                float shimmer = 0.82f + MathF.Sin(time * 5.1f + i * 1.73f) * 0.12f;
                float opacity = fadeIn * fadeOut * shimmer;
                float size = (2f + starMotes[i].Scale * 2f) * (1f + completion * 0.2f);
                Vector2 drawPosition = starMotes[i].Position - Main.screenPosition;
                Color color = starMotes[i].Color * (0.30f * opacity);

                spriteBatch.Draw(texture, drawPosition, source, color, starMotes[i].Rotation, origin,
                    new Vector2(size) / dimensions, SpriteEffects.None, 0f);
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
            (uint)line.To < Stars.Length &&
            IsStarUnlocked(Stars[line.From]) && IsStarUnlocked(Stars[line.To]);

        private Vector2 GetStarOffset(ConstellationStar star)
        {
            Vector2 offset = star.Offset.RotatedBy(ConstellationTilt);
            offset.X *= Projectile.spriteDirection;
            return offset;
        }

        private Vector2 GetStarPosition(int starIndex, Vector2 center) =>
            center + GetStarOffset(Stars[starIndex]) * Projectile.scale;

        private void DrawStarBackgrounds(Vector2 center)
        {
            if (!AntaresVisualAssetSystem.HasSoftStarMote)
                return;
            Texture2D soft = AntaresVisualAssetSystem.SoftStarMote;
            for (int i = 0; i < Stars.Length; i++)
            {
                if (starReveal[i] <= 0f)
                    continue;
                bool detail = i >= 14;
                float diameter = i == 0 ? 14f : detail ? 6f : 10f;
                float opacity = i == 0 ? 0.14f : detail ? 0.055f : 0.09f;
                Main.spriteBatch.Draw(soft, GetStarPosition(i, center) - Main.screenPosition, null,
                    Stars[i].GlowColor * (starReveal[i] * opacity), 0f, soft.Size() * 0.5f,
                    diameter / soft.Width, SpriteEffects.None, 0f);
            }
        }

        private void DrawStarSpikes(Vector2 worldPos, int index, float time, float burstRelease)
        {
            if (!AntaresVisualAssetSystem.HasSpikeMask)
                return; // 亮核和柔光仍保留，不把缺失遮罩画成白色方块。
            Texture2D mask = AntaresVisualAssetSystem.SpikeMask;
            float twinkle = 0.95f + MathF.Sin(time * (0.9f + index * 0.07f) + index * 1.37f) * 0.05f;
            float radius = index == 0 ? 10f + burstRelease * 4f
                : index == 7 ? 5f : index >= 14 ? 3f : 3.4f + Stars[index].Size;
            if (index == 0 && shotAge < 2)
                radius *= 1f - (2f - shotAge) * 0.05f;
            Color color = index == 0
                ? Color.Lerp(new Color(255, 150, 70), new Color(125, 205, 255), burstRelease)
                : Stars[index].CoreColor;
            float opacity = starReveal[index] * twinkle * (index == 0 ? 0.82f : index >= 14 ? 0.48f : 0.72f);
            if (index == 0)
                opacity *= 1f + Math.Max(ShotEnvelope(shotAge), burstRelease) * 0.15f;
            Vector2 position = worldPos - Main.screenPosition;
            Vector2 scale = new Vector2(radius * 2f) / mask.Size();
            float angle = ConstellationTilt * Projectile.spriteDirection;
            Main.spriteBatch.Draw(mask, position, null, color * opacity, angle,
                mask.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            if (index == 0 && visualTier > 0f)
            {
                float phase = 0.8f + MathF.Sin(time * 0.7f + 0.6f) * 0.2f;
                float layer = MathHelper.Clamp(visualTier, 0f, 1f);
                Main.spriteBatch.Draw(mask, position, null, color * (opacity * 0.22f * layer * phase),
                    angle + MathHelper.PiOver4, mask.Size() * 0.5f, scale * new Vector2(0.75f, 0.52f),
                    SpriteEffects.None, 0f);
            }
        }

        private void DrawStarCore(Vector2 worldPos, int index, float time, float burstRelease)
        {
            float twinkle = 0.96f + MathF.Sin(time * 1.1f + index * 1.71f) * 0.04f;
            float energy = index > 0 && index <= 7
                ? BodyEnvelope(BodyDistances[index], time) * MathHelper.Clamp(visualTier - 1f, 0f, 1f) * 0.10f : 0f;
            Color color = index == 0
                ? Color.Lerp(new Color(255, 232, 195), new Color(215, 242, 255), burstRelease)
                : Color.Lerp(Stars[index].CoreColor, Color.White, index >= 14 ? 0.12f : 0.30f);
            float size = index == 0 ? 2f : index >= 14 ? 1f : index == 7 ? 1.7f : 1.4f;
            Main.spriteBatch.Draw(Terraria.GameContent.TextureAssets.MagicPixel.Value,
                worldPos - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                color * (starReveal[index] * twinkle * (index == 0 ? 0.95f : 0.72f + energy)),
                0f, new Vector2(0.5f), size, SpriteEffects.None, 0f);
        }

        private void DrawTransientFeedback()
        {
            Texture2D texture = AntaresVisualAssetSystem.HasSpikeMask
                ? AntaresVisualAssetSystem.SpikeMask : AntaresVisualAssetSystem.SoftStarMote;
            bool textured = AntaresVisualAssetSystem.HasSpikeMask || AntaresVisualAssetSystem.HasSoftStarMote;
            Rectangle? source = textured ? null : new Rectangle(0, 0, 1, 1);
            Vector2 origin = textured ? texture.Size() * 0.5f : new Vector2(0.5f);
            Vector2 dimensions = textured ? texture.Size() : Vector2.One;
            foreach (StarMote spark in transientSparks)
            {
                if (!spark.Active)
                    continue;
                float fade = 1f - spark.Time / spark.Lifetime;
                Main.spriteBatch.Draw(texture, spark.Position - Main.screenPosition, source,
                    spark.Color * (fade * fade * 0.65f), spark.Rotation, origin,
                    new Vector2(8f, 3f) * spark.Scale / dimensions, SpriteEffects.None, 0f);
            }
            foreach (ImpactFlash flash in impactFlashes)
            {
                if (!flash.Active)
                    continue;
                float fade = 1f - flash.Age / (float)ImpactVisualTicks;
                Color color = flash.Burst ? new Color(165, 220, 255) : new Color(255, 180, 80);
                float diameter = (flash.Burst ? 18f : 12f) * (0.8f + (1f - fade) * 0.4f);
                Main.spriteBatch.Draw(texture, flash.Position - Main.screenPosition, source,
                    color * (fade * fade * 0.85f), flash.Rotation, origin,
                    new Vector2(diameter, diameter * 0.6f) / dimensions, SpriteEffects.None, 0f);
            }
        }

        private void DrawAntaresShaderStar(Vector2 worldPos, float time, float opacity, float release)
        {
            if (!ShaderManager.TryGetShader("TestMod.AntaresStarShader", out ManagedShader shader))
                return;
            shader.TrySetParameter("globalTime", time);
            shader.TrySetParameter("starTier", visualTier);
            shader.TrySetParameter("starIntensity", opacity);
            shader.TrySetParameter("shotEnvelope", release);
            shader.TrySetParameter("shotContraction", shotAge < 2 ? (2f - shotAge) * 0.5f : 0f);
            shader.TrySetParameter("noisePresence", AntaresVisualAssetSystem.HasHaloNoise ? 1f : 0f);
            shader.TrySetParameter("rampPresence", AntaresVisualAssetSystem.HasRadialRamp ? 1f : 0f);
            shader.SetTexture(AntaresVisualAssetSystem.HaloNoise, 2, SamplerState.LinearWrap);
            shader.SetTexture(AntaresVisualAssetSystem.RadialRamp, 4, SamplerState.LinearClamp);
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Vector2 size = new(28f);
            // RenderQuad的scale会乘源纹理尺寸，归一化后保证半径14，与MagicPixel实际尺寸无关。
            Vector2 anchor = worldPos - new Vector2(size.X * 0.5f, -size.Y * 0.5f);
            PrimitiveRenderer.RenderQuad(pixel, anchor, size / pixel.Size(), 0f, Color.White, shader);
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(Projectile.minionSlots);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Projectile.minionSlots = reader.ReadSingle();
            // 首次收到既有实体时直接显示快照，之后收到槽位增长仍走逐星淡入。
            if (!visualInitialized)
                remoteSnapshot = true;
        }
    }
}
