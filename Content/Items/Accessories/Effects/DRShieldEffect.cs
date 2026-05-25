using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Content.Projectiles.Accessories;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  DRShieldEffect  ——  伤害减免护盾效果（灾厄格挡风格）
    // ----------------------------------------------------------------------------
    //  多颗弹幕环绕玩家，检测到敌方射弹进入拦截半径时标记它；
    //  被标记的射弹命中玩家时在 ModifyHitPlayer 中套用减免。
    //  弹幕有耐久，被拦截时扣减，归零后破碎并进入重生冷却。
    // ============================================================================

    public struct DRShieldConfig
    {
        // ── 轨道参数 ──
        public int   OrbCount;         // 弹幕数量        默认 3
        public float OrbRadius;        // 轨道半径 px     默认 90
        public float RotationSpeed;    // 旋转速度 rad/帧  默认 0.03
        // ── 拦截参数 ──
        public float DetectionRadius;  // 拦截半径 px     默认 20
        public int   FlatDR;           // 固定减免伤害    默认 40
        public float MultDR;           // 乘法减免 0~1    默认 0.15
        // ── 耐久参数 ──
        public int   OrbHP;            // 每颗球耐久      默认 120
        public int   RespawnCooldown;  // 破碎后重生冷却帧 默认 300（5秒）

        public static DRShieldConfig Default => new DRShieldConfig
        {
            OrbCount        = 3,
            OrbRadius       = 90f,
            RotationSpeed   = 0.03f,
            DetectionRadius = 20f,
            FlatDR          = 40,
            MultDR          = 0.15f,
            OrbHP           = 120,
            RespawnCooldown = 300,
        };
    }

    public static class DRShieldEffect
    {
        // ── 在 UpdateAccessory 中调用 ─────────────────────────────────
        public static void Apply(Player player, DRShieldConfig? config = null)
        {
            var mp = player.GetModPlayer<OmniEffectsPlayer>();
            mp.EnableDRShield  = true;
            mp.DRShieldConfig  = config ?? DRShieldConfig.Default;
        }

        // ── 由 OmniEffectsPlayer.PostUpdateMiscEffects 每帧调用 ───────
        public static void UpdateMiscEffects(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnableDRShield) return;

            // 推进旋转角
            mp.DRShieldRotationAngle += mp.DRShieldConfig.RotationSpeed;

            // 冷却中不生成
            if (mp.DRShieldRespawnCooldown > 0) return;

            // 只由本地玩家客户端生成（multiplayer 安全模式）
            if (Main.myPlayer != player.whoAmI) return;

            int orbType = ModContent.ProjectileType<DRShieldOrb>();
            int count   = mp.DRShieldConfig.OrbCount;

            // 找出哪些索引槽已被占用
            bool[] occupied = new bool[count];
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != orbType || p.owner != player.whoAmI) continue;
                int idx = (int)p.ai[1];
                if (idx >= 0 && idx < count)
                    occupied[idx] = true;
            }

            // 补齐缺失的弹幕
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
