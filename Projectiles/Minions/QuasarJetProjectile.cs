using System;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;

namespace TestMod.Projectiles.Minions
{
    public class QuasarJetProjectile : ModProjectile
    {
        // 默认持续时长（帧）——发射后的生命周期，影响淡入淡出与命中判定时长
        private const int DefaultDuration = 120;
        // 光束长度（像素），用于射线检测和可视化长度
        private const float BeamLength = 3200f;
        // 碰撞检测宽度（像素），用于 AABB vs 线段 的碰撞判定
        private const float CollisionWidth = 55f;
        // 光束最外层宽度（像素），用于绘制外围的发光层
        private const float OuterBeamWidth = 60f;
        // 光束中层宽度（像素），用于绘制中间亮带
        private const float MiddleBeamWidth = 34f;
        // 光束核心宽度（像素），用于绘制最亮的内核
        private const float CoreBeamWidth = 9f;
        // 用于纹理绘制的光束宽度（像素），当使用纹理贴图替代基础像素绘制时使用
        private const float TextureBeamWidth = 40f;
        // 发射点相对于父实体中心的偏移半径（像素），决定光束从父实体哪儿发出
        private const float ParentEmissionRadius = 48f;
        // 索敌范围（像素），在此半径内寻找目标用于跟踪
        private const float TargetSearchRange = 1800f;
        // 方向跟踪强度（0-1），控制光束朝目标转向时的平滑跟随速率
        private const float DirectionTrackingStrength = 0.22f;
        // 淡入时间（帧），影响可视透明度从 0 到 1 的过渡
        private const float FadeInTime = 5f;
        // 淡出时间（帧），影响生命周期结束时透明度从 1 到 0 的过渡
        private const float FadeOutTime = 10f;
        // 原始线段 primitive 每段细分的采样点数，用于原始图元绘制质量控制
        private const int PrimitivePointsPerSegment = 16;

        private static readonly Color JetColor = new(55, 160, 255);
        private static readonly Color HotCoreColor = new(235, 250, 255);

        public override string Texture => "Terraria/Images/Projectile_12";

        private Vector2 BeamDirection => Projectile.ai[0].ToRotationVector2().SafeNormalize(Vector2.UnitX);

        private float Duration => Projectile.localAI[1] <= 0f ? DefaultDuration : Projectile.localAI[1];

        private float Age => Duration - Projectile.timeLeft;

        private float VisualOpacity
        {
            get
            {
                float fadeIn = MathHelper.Clamp(Age / FadeInTime, 0f, 1f);
                float fadeOut = MathHelper.Clamp(Projectile.timeLeft / FadeOutTime, 0f, 1f);
                return fadeIn * fadeOut;
            }
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.timeLeft = DefaultDuration;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                Projectile.localAI[1] = Projectile.ai[1] > 0f ? Projectile.ai[1] : DefaultDuration;
                Projectile.timeLeft = (int)Projectile.localAI[1];
                Projectile.netUpdate = true;
            }

            UpdateParentedBeam();

            Vector2 direction = BeamDirection;
            Projectile.velocity = direction;
            Projectile.rotation = direction.ToRotation();

            float opacity = VisualOpacity;
            Lighting.AddLight(Projectile.Center, 0.34f * opacity, 0.72f * opacity, 1.45f * opacity);

            if (Main.rand.NextBool(3))
                SpawnJetDust(direction, opacity);
        }

        private void UpdateParentedBeam()
        {
            int encodedParent = (int)MathF.Round(Projectile.ai[2]);
            if (encodedParent == 0)
                return;

            int parentIndex = Math.Abs(encodedParent) - 1;
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIndex];
            if (!parent.active || parent.owner != Projectile.owner || parent.type != ModContent.ProjectileType<BlackHoleMinion>())
            {
                Projectile.Kill();
                return;
            }

            float directionSign = encodedParent < 0 ? -1f : 1f;
            Vector2 desiredDirection = BeamDirection;
            NPC target = FindTrackedTarget(parent.Center);
            if (target is not null)
                desiredDirection = (target.Center - parent.Center).SafeNormalize(desiredDirection) * directionSign;

            float desiredRotation = desiredDirection.ToRotation();
            Projectile.ai[0] += MathHelper.WrapAngle(desiredRotation - Projectile.ai[0]) * DirectionTrackingStrength;

            Vector2 direction = BeamDirection;
            Projectile.Center = parent.Center + direction * ParentEmissionRadius;
        }

        private NPC FindTrackedTarget(Vector2 origin)
        {
            Player owner = Main.player[Projectile.owner];

            if (owner.HasMinionAttackTargetNPC)
            {
                NPC forced = Main.npc[owner.MinionAttackTargetNPC];
                if (forced.CanBeChasedBy(Projectile) && Vector2.Distance(origin, forced.Center) <= TargetSearchRange * 1.5f)
                    return forced;
            }

            NPC bestTarget = null;
            float bestDistance = TargetSearchRange;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float distance = Vector2.Distance(origin, npc.Center);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestTarget = npc;
            }

            return bestTarget;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (VisualOpacity <= 0.18f)
                return false;

            float collisionPoint = 0f;
            Vector2 targetPosition = new(targetHitbox.X, targetHitbox.Y);
            Vector2 targetSize = new(targetHitbox.Width, targetHitbox.Height);
            return Collision.CheckAABBvLineCollision(
                targetPosition,
                targetSize,
                Projectile.Center,
                Projectile.Center + BeamDirection * BeamLength,
                CollisionWidth * VisualOpacity,
                ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            Player owner = Main.player[Projectile.owner];
            var summonCritPlayer = owner.GetModPlayer<SummonCritPlayer>();
            modifiers.ScalingArmorPenetration += 0.9f;
            if (!summonCritPlayer.Enabled)
                SummonCritPlayer.TryApplySummonCrit(owner, Projectile, ref modifiers, requireEnabled: false);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            DrawBeam();
            return false;
        }

        private void DrawBeam()
        {
            Vector2 start = Projectile.Center;
            Vector2 direction = BeamDirection;
            Vector2 end = start + direction * BeamLength;
            float opacity = VisualOpacity;

            DrawBeamPrimitives(start, end, opacity);
            DrawTexturedBeam(start, end, direction, opacity);

            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        private static void DrawBeamPrimitives(Vector2 start, Vector2 end, float opacity)
        {
            Vector2[] points = [start, end];
            DrawUtils.PrepareForAdditivePrimitives(Main.spriteBatch);

            PrimitiveRenderer.RenderTrail(
                points,
                new PrimitiveSettings(
                    completion => BeamWidth(completion, OuterBeamWidth),
                    completion => BeamColor(completion, JetColor, 0.26f * opacity),
                    Smoothen: false),
                PrimitivePointsPerSegment);

            PrimitiveRenderer.RenderTrail(
                points,
                new PrimitiveSettings(
                    completion => BeamWidth(completion, MiddleBeamWidth),
                    completion => BeamColor(completion, JetColor, 0.68f * opacity),
                    Smoothen: false),
                PrimitivePointsPerSegment);

            PrimitiveRenderer.RenderTrail(
                points,
                new PrimitiveSettings(
                    completion => BeamWidth(completion, CoreBeamWidth),
                    completion => BeamColor(completion, HotCoreColor, 1.15f * opacity),
                    Smoothen: false),
                PrimitivePointsPerSegment);
        }

        private static float BeamWidth(float completion, float width)
        {
            float endFade = MathF.Sin(MathHelper.Clamp(completion, 0f, 1f) * MathHelper.Pi);
            return width * MathHelper.Lerp(0.28f, 1f, MathHelper.Clamp(endFade * 1.25f, 0f, 1f));
        }

        private static Color BeamColor(float completion, Color color, float opacity)
        {
            float endFade = MathF.Sin(MathHelper.Clamp(completion, 0f, 1f) * MathHelper.Pi);
            return color * (opacity * MathHelper.Clamp(endFade * 1.2f, 0f, 1f));
        }

        private void DrawTexturedBeam(Vector2 start, Vector2 end, Vector2 direction, float opacity)
        {
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.LinearWrap,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix);

            Texture2D trailTexture = QuasarJetVisualAssetSystem.TrailTexture;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float length = Vector2.Distance(start, end);
            float rotation = direction.ToRotation();
            Vector2 center = (start + end) * 0.5f - Main.screenPosition;

            if (QuasarJetVisualAssetSystem.HasFineBeamMaterial)
            {
                DrawFineMaterialBeam(pixel, center, rotation, length, opacity);
                DrawOriginFlare(start - Main.screenPosition, rotation, opacity);
                DrawTerminalGlow(end - Main.screenPosition, rotation, opacity);
                Main.spriteBatch.End();
                return;
            }

            if (QuasarJetVisualAssetSystem.HasTrailTexture)
            {
                Rectangle frame = trailTexture.Frame();
                Vector2 scale = new(length / frame.Width, TextureBeamWidth / frame.Height);
                Main.spriteBatch.Draw(trailTexture, center, frame, JetColor * (0.58f * opacity), rotation, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
            else
            {
                Main.spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), JetColor * (0.58f * opacity), rotation, new Vector2(0.5f), new Vector2(length, TextureBeamWidth), SpriteEffects.None, 0f);
            }

            DrawOriginFlare(start - Main.screenPosition, rotation, opacity);
            DrawTerminalGlow(end - Main.screenPosition, rotation, opacity);
            Main.spriteBatch.End();
        }

        private static void DrawFineMaterialBeam(Texture2D pixel, Vector2 center, float rotation, float length, float opacity)
        {
            float time = Main.GlobalTimeWrappedHourly;

            if (QuasarJetVisualAssetSystem.HasBeamBodyTexture)
            {
                DrawWrappedBeamLayer(
                    QuasarJetVisualAssetSystem.BeamBodyTexture,
                    center,
                    rotation,
                    length,
                    TextureBeamWidth,
                    JetColor,
                    0.78f * opacity,
                    1.35f,
                    time * 620f);
            }
            else
            {
                Main.spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), JetColor * (0.52f * opacity), rotation, new Vector2(0.5f), new Vector2(length, TextureBeamWidth), SpriteEffects.None, 0f);
            }

            if (QuasarJetVisualAssetSystem.HasBeamFlowTexture)
            {
                DrawWrappedBeamLayer(
                    QuasarJetVisualAssetSystem.BeamFlowTexture,
                    center,
                    rotation,
                    length,
                    TextureBeamWidth * 1.16f,
                    Color.White,
                    0.52f * opacity,
                    1.9f,
                    time * -980f,
                    time * 70f);
            }

            if (QuasarJetVisualAssetSystem.HasBeamCoreTexture)
            {
                DrawWrappedBeamLayer(
                    QuasarJetVisualAssetSystem.BeamCoreTexture,
                    center,
                    rotation,
                    length,
                    CoreBeamWidth * 2.35f,
                    HotCoreColor,
                    1f * opacity,
                    1.05f,
                    time * 820f);
            }
            else
            {
                Main.spriteBatch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), HotCoreColor * (0.95f * opacity), rotation, new Vector2(0.5f), new Vector2(length, CoreBeamWidth * 1.6f), SpriteEffects.None, 0f);
            }
        }

        private static void DrawWrappedBeamLayer(Texture2D texture, Vector2 center, float rotation, float length, float width, Color color, float opacity, float pixelsPerWorldUnit, float scrollX, float scrollY = 0f)
        {
            if (texture.Width <= 0 || texture.Height <= 0)
                return;

            int sourceWidth = Math.Max(texture.Width, (int)MathF.Ceiling(length * pixelsPerWorldUnit));
            int sourceX = WrappedTextureOffset(scrollX, texture.Width);
            int sourceY = WrappedTextureOffset(scrollY, texture.Height);
            Rectangle source = new(sourceX, sourceY, sourceWidth, texture.Height);
            Vector2 scale = new(length / source.Width, width / source.Height);
            Main.spriteBatch.Draw(texture, center, source, color * opacity, rotation, source.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        private static int WrappedTextureOffset(float value, int size)
        {
            if (size <= 1)
                return 0;

            int offset = (int)value % size;
            return offset < 0 ? offset + size : offset;
        }

        private static void DrawOriginFlare(Vector2 position, float rotation, float opacity)
        {
            if (!QuasarJetVisualAssetSystem.HasGlowTexture)
                return;

            Texture2D glowTexture = QuasarJetVisualAssetSystem.GlowTexture;
            Rectangle frame = glowTexture.Frame();
            Vector2 origin = new(frame.Width * 0.28f, frame.Height * 0.5f);
            Main.spriteBatch.Draw(glowTexture, position, frame, JetColor * (0.78f * opacity), rotation, origin, new Vector2(1.85f, 1.15f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glowTexture, position, frame, HotCoreColor * (0.52f * opacity), rotation, origin, new Vector2(1.05f, 0.42f), SpriteEffects.None, 0f);
        }

        private static void DrawTerminalGlow(Vector2 position, float rotation, float opacity)
        {
            if (!QuasarJetVisualAssetSystem.HasGlowTexture)
                return;

            Texture2D glowTexture = QuasarJetVisualAssetSystem.GlowTexture;
            Rectangle frame = glowTexture.Frame();
            Vector2 origin = new(frame.Width * 0.72f, frame.Height * 0.5f);
            Main.spriteBatch.Draw(glowTexture, position, frame, JetColor * (0.32f * opacity), rotation, origin, new Vector2(1.2f, 0.72f), SpriteEffects.None, 0f);
        }

        private void SpawnJetDust(Vector2 direction, float opacity)
        {
            if (opacity <= 0.1f)
                return;

            Vector2 normal = direction.RotatedBy(MathHelper.PiOver2);
            Vector2 position = Projectile.Center
                + direction * Main.rand.NextFloat(24f, BeamLength * 0.72f)
                + normal * Main.rand.NextFloat(-CollisionWidth, CollisionWidth);
            Dust dust = Dust.NewDustPerfect(position, DustID.Electric, direction * Main.rand.NextFloat(1.6f, 4.2f), 80, JetColor, Main.rand.NextFloat(0.7f, 1.35f));
            dust.noGravity = true;
        }
    }
}
