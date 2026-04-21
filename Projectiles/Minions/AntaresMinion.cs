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
    public class AntaresMinion : ModProjectile
    {
        public Player Owner => Main.player[Projectile.owner];
        public AntaresMinionPlayer ModdedOwner => Owner.GetModPlayer<AntaresMinionPlayer>();

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
        }

        private void SpawnStarDust()
        {
            Vector2 center = Projectile.Center;

            void Star(float slotReq, Vector2 offset, float intensity, int dustType = DustID.BlueTorch) // ← 加参数，默认蓝色
            {
                if (slotReq > 0 && Projectile.minionSlots < slotReq)
                    return;
                offset.X *= Projectile.spriteDirection;
                Vector2 pos = center + offset * Projectile.scale;
                if (Main.rand.NextBool(2))
                {
                    int d = Dust.NewDust(pos - Vector2.One * 2f, 4, 4, dustType, 0f, 0f, 100, default, intensity); // ← 用参数
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity *= 0.2f;
                    Main.dust[d].scale = intensity * Main.rand.NextFloat(1f, 1.4f);
                }
            }

            // ─── Scorpius 天蝎座 (以 α Antares 心宿二为中心) ───
            Star(0, new Vector2(0f, 0f), 1.5f, DustID.RedTorch);  // α Antares (心宿二,主星)    红色，其他不传就默认蓝
            Star(2, new Vector2(70f, -80f), 0.75f); // δ Sco    (房宿三,头)
            Star(3, new Vector2(-30f, 80f), 0.75f); // τ Sco    (身躯上段)
            Star(4, new Vector2(-80f, 150f), 0.75f); // ε Sco    (身躯中段)
            Star(5, new Vector2(140f, -140f), 0.75f); // β Sco    (房宿四,头顶)
            Star(6, new Vector2(-120f, 210f), 0.5f);  // μ Sco    (尾部起点)
            Star(7, new Vector2(-170f, 240f), 0.5f);  // ζ Sco    (钩底拐点)
            Star(8, new Vector2(-210f, 200f), 0.5f);  // θ Sco    (钩外最远端)
            Star(9, new Vector2(-140f, 170f), 0.75f); // λ Sco    (Shaula 毒针)
            Star(10, new Vector2(190f, -100f), 0.5f);  // ρ Sco    (右爪)
            Star(11, new Vector2(80f, -160f), 0.5f);  // π Sco    (左爪)
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

            for (int i = 0; i < burstCount; i++)
            {
                if (burstCount > 3)
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.3f }, Projectile.Center); // 触发爆发时播放不同音效
                else
                    SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.15f }, Projectile.Center); // 原来的音效
                Vector2 velocity = new Vector2(25f, 0f).RotatedByRandom(MathHelper.Pi);
                float damageMod = 1f + MathF.Pow(0.06f * Projectile.minionSlots, 1.5f);
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

            // ─── 身躯主线: 从头部经心宿二到毒针 ───
            Connect(2, new Vector2(0f, 0f), new Vector2(70f, -80f)); // Antares → δ
            Connect(3, new Vector2(0f, 0f), new Vector2(-30f, 80f)); // Antares → τ
            Connect(4, new Vector2(-30f, 80f), new Vector2(-80f, 150f)); // τ → ε
            Connect(5, new Vector2(70f, -80f), new Vector2(140f, -140f)); // δ → β
            Connect(6, new Vector2(-80f, 150f), new Vector2(-120f, 210f)); // ε → μ
            Connect(7, new Vector2(-120f, 210f), new Vector2(-170f, 240f)); // μ → ζ 入钩
            Connect(8, new Vector2(-170f, 240f), new Vector2(-210f, 200f)); // ζ → θ 钩外
            Connect(9, new Vector2(-210f, 200f), new Vector2(-140f, 170f)); // θ → λ 毒针
            Connect(10, new Vector2(140f, -140f), new Vector2(190f, -100f)); // β → ρ 右爪
            Connect(11, new Vector2(140f, -140f), new Vector2(80f, -160f)); // β → π 左爪

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