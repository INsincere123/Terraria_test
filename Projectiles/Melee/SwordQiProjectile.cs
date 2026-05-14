using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using TestMod.Items.Weapons;

namespace TestMod.Projectiles.Melee
{
    // ============================================================================
    //  SwordQiProjectile  ——  剑气弹幕
    // ----------------------------------------------------------------------------
    //  ai[0]      = 目标 NPC 的 whoAmI
    //  ai[1]      = 摆动计时器（每帧 +1，驱动正弦波）
    //  localAI[0] = 当前叠层数（0 = 初始层）
    //
    //  运作流程：
    //    每帧定位到目标中心 + 正弦偏移（垂直于玩家→目标轴）
    //    → usesLocalNPCImmunity 控制打击频率
    //    → 叠层驱动伤害曲线（Sigmoid + 对数）与打击频率加快
    //    → 目标死亡或 timeLeft 归零时自毁
    //
    //  双判定原理：
    //    localNPCHitCooldown = 9~20 帧；大体积 boss 的 Hitbox 足够宽，
    //    弹幕摆过去时冷却期内仍在 Hitbox 内，自然触发第二次判定，无需特判
    // ============================================================================

    public class SwordQiProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_946";

        // ── 数值调节区 ────────────────────────────────────────────────
        public static float OscillationRadius  = 150f;  // 摆动半径最小值（px），对大体积敌人自动扩大
        public static float OscillationPadding = 60f;   // 在敌人半身宽基础上额外延伸的距离（px）
        public static float OscillationSpeed   = 0.24f; // 摆动速度（rad/帧），越大越快
        public static float MaxEarlyBonus      = 1.5f;  // 20层时最大伤害加成（×2.5 总计）
        public static float MaxLateBonus       = 0.3f;  // 100层时额外加成上限
        // ─────────────────────────────────────────────────────────────

        private int Stacks => (int)Projectile.localAI[0];

        public override void SetDefaults()
        {
            Projectile.width                = 40;
            Projectile.height               = 40;
            Projectile.friendly             = true;
            Projectile.hostile              = false;
            Projectile.tileCollide          = false;
            Projectile.ignoreWater          = true;
            Projectile.penetrate            = -1;
            Projectile.timeLeft             = SwordQiSword.QiDuration;
            Projectile.DamageType           = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown  = 2;    //占位，实际在 AI 中根据叠层数调整
            Projectile.ArmorPenetration     = 10;   // 固定穿甲
        }

        public override void AI()
        {
            int npcIndex = (int)Projectile.ai[0];
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
            {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[npcIndex];
            if (!target.active || target.life <= 0)
            {
                Projectile.Kill();
                return;
            }

            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            // ── 叠层驱动打击冷却 ─────────────────────────────────────
            Projectile.localNPCHitCooldown = Stacks < 10 ? 16 : Stacks < 20 ? 9 : 4;

            // ── 摆动轴：垂直于玩家→目标方向 ──────────────────────────
            Projectile.ai[1] += 1f;
            Vector2 toTarget  = target.Center - owner.Center;
            float   axisAngle = toTarget == Vector2.Zero
                              ? 0f
                              : toTarget.ToRotation() + MathHelper.PiOver2;
            float sine = MathF.Sin(Projectile.ai[1] * OscillationSpeed);

            // 根据目标体积动态计算半径，保底 OscillationRadius
            float halfMax = MathF.Max(target.width, target.height) * 0.5f;
            float radius  = MathF.Max(OscillationRadius, halfMax + OscillationPadding);

            Projectile.Center   = target.Center + new Vector2(radius * sine, 0f).RotatedBy(axisAngle);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = axisAngle; // 贴图朝向沿摆动轴（垂直切割感）
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.SourceDamage *= GetDamageMultiplier(Stacks);
        }

        // ── Sigmoid（0-20层快速爬升）+ 对数（20-100层极缓增长）────────
        private static float GetDamageMultiplier(int stacks)
        {
            // Sigmoid 标准化：n=0→0，n=20→1
            static float Sig(float n) => 1f / (1f + MathF.Exp(-0.45f * (n - 15f)));
            float s0    = Sig(0f);
            float s20   = Sig(20f);
            float earlyT = Math.Clamp((Sig(stacks) - s0) / (s20 - s0), 0f, 1f);

            // 对数：stacks=20→0，stacks=100→1，增长极缓慢
            float lateT = stacks <= 20
                ? 0f
                : Math.Clamp(MathF.Log(1f + stacks - 20f) / MathF.Log(81f), 0f, 1f);

            return 1f + MaxEarlyBonus * earlyT + MaxLateBonus * lateT;
        }

        // ── 描边变色 + 20层后 Additive 发光 ─────────────────────────────
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex    = TextureAssets.Projectile[946].Value;
            Vector2   origin = tex.Size() / 2f;
            Vector2   pos    = Projectile.Center - Main.screenPosition;
            float     rot    = Projectile.rotation;
            float     scale  = Projectile.scale;

            Color outlineColor = GetOutlineColor(Stacks);
            const float T = 2f; // 描边厚度（px）

            // ── 四向偏移描边（普通混合）──────────────────────────────
            Main.spriteBatch.Draw(tex, pos + new Vector2( T,  0), null, outlineColor, rot, origin, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pos + new Vector2(-T,  0), null, outlineColor, rot, origin, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pos + new Vector2( 0,  T), null, outlineColor, rot, origin, scale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, pos + new Vector2( 0, -T), null, outlineColor, rot, origin, scale, SpriteEffects.None, 0f);

            // ── 主贴图（覆盖在描边上方）──────────────────────────────
            Main.spriteBatch.Draw(tex, pos, null, lightColor, rot, origin, scale, SpriteEffects.None, 0f);

            // ── 20层后：Additive 发光描边 ────────────────────────────
            if (Stacks >= 20)
            {
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                    Main.DefaultSamplerState, null, null, null,
                    Main.GameViewMatrix.TransformationMatrix);

                Color glow = Color.CornflowerBlue * 0.6f;
                Main.spriteBatch.Draw(tex, pos + new Vector2( T,  0), null, glow, rot, origin, scale, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(tex, pos + new Vector2(-T,  0), null, glow, rot, origin, scale, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(tex, pos + new Vector2( 0,  T), null, glow, rot, origin, scale, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(tex, pos + new Vector2( 0, -T), null, glow, rot, origin, scale, SpriteEffects.None, 0f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    Main.DefaultSamplerState, null, null, null,
                    Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        private static Color GetOutlineColor(int stacks)
        {
            if (stacks <= 10)
                return Color.White * 0.4f;

            if (stacks <= 20)
            {
                float t = (stacks - 10f) / 10f; // 0→1
                return Color.Lerp(Color.Gold, Color.BlueViolet, t);
            }

            return Color.CornflowerBlue;
        }
    }
}
