using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Projectiles.Accessories;

namespace TestMod.Items.Accessories.Effects
{
    // ============================================================================
    //  ReflectShieldEffect  ——  反射护盾效果（Fargo 伞盾风格）
    // ----------------------------------------------------------------------------
    //  多颗弹幕环绕玩家，与敌方射弹碰撞时改变其归属并反转速度（或直接消除）。
    //  弹幕有耐久，拦截后扣减（方案A：先完整处理再检查是否破碎）。
    // ============================================================================

    public struct ReflectShieldConfig
    {
        // ── 轨道参数 ──
        public int   OrbCount;          // 弹幕数量        默认 3
        public float OrbRadius;         // 轨道半径 px     默认 110
        public float RotationSpeed;     // 旋转速度 rad/帧  默认 0.025
        // ── 拦截参数 ──
        public bool  ShouldReflect;     // true=反弹 false=消除  默认 true
        public float ReflectDamageMult; // 反弹后伤害倍率  默认 1.5
        // ── 耐久参数 ──
        public int   OrbHP;             // 每颗球耐久      默认 150
        public int   RespawnCooldown;   // 破碎后重生冷却帧 默认 420（7秒）

        public static ReflectShieldConfig Default => new ReflectShieldConfig
        {
            OrbCount          = 3,
            OrbRadius         = 110f,
            RotationSpeed     = 0.025f,
            ShouldReflect     = true,
            ReflectDamageMult = 1.5f,
            OrbHP             = 150,
            RespawnCooldown   = 420,
        };
    }

    public static class ReflectShieldEffect
    {
        // ── 在 UpdateAccessory 中调用 ─────────────────────────────────
        public static void Apply(Player player, ReflectShieldConfig? config = null)
        {
            var mp = player.GetModPlayer<OmniEffectsPlayer>();
            mp.EnableReflectShield  = true;
            mp.ReflectShieldConfig  = config ?? ReflectShieldConfig.Default;
        }

        // ── 由 OmniEffectsPlayer.PostUpdateMiscEffects 每帧调用 ───────
        public static void UpdateMiscEffects(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnableReflectShield) return;

            mp.ReflectShieldRotationAngle += mp.ReflectShieldConfig.RotationSpeed;

            if (mp.ReflectShieldRespawnCooldown > 0) return;
            if (Main.myPlayer != player.whoAmI) return;

            int orbType = ModContent.ProjectileType<ReflectShieldOrb>();
            int count   = mp.ReflectShieldConfig.OrbCount;

            bool[] occupied = new bool[count];
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != orbType || p.owner != player.whoAmI) continue;
                int idx = (int)p.ai[1];
                if (idx >= 0 && idx < count)
                    occupied[idx] = true;
            }

            for (int i = 0; i < count; i++)
            {
                if (!occupied[i])
                {
                    Projectile.NewProjectile(
                        player.GetSource_FromThis(),
                        player.Center, Vector2.Zero,
                        orbType, 0, 0f, player.whoAmI,
                        ai0: 0f, ai1: i);
                }
            }
        }
    }
}
