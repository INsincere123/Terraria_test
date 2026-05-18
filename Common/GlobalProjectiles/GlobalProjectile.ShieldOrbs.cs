using Terraria;
using Terraria.ModLoader;
using TestMod.Items.Accessories.Effects;

namespace TestMod.Common.GlobalProjectiles
{
    /// <summary>
    /// 护盾弹幕系统（GlobalProjectile 层）
    ///   - MarkedForDRShield：被 DRShieldOrb 标记，命中玩家时套用减免
    ///   - AlreadyReflected：被 ReflectShieldOrb 处理过，防止重复拦截
    /// </summary>
    public partial class GlobalProjectile : Terraria.ModLoader.GlobalProjectile
    {
        // ── 实例字段（InstancePerEntity=true，每颗弹幕独立）──────────
        public bool MarkedForDRShield; // DRShieldOrb 标记此射弹已被拦截
        public bool AlreadyReflected;  // ReflectShieldOrb 标记此射弹已被处理

        // ── 命中玩家时套用伤害减免 ────────────────────────────────────
        public override void ModifyHitPlayer(Projectile projectile, Player target,
            ref Player.HurtModifiers modifiers)
        {
            if (!MarkedForDRShield) return;

            var mp = target.GetModPlayer<OmniEffectsPlayer>();
            if (!mp.EnableDRShield) return;

            // 固定减免（先减）
            if (mp.DRShieldConfig.FlatDR > 0)
                modifiers.FinalDamage.Flat -= mp.DRShieldConfig.FlatDR;

            // 乘法减免（后乘）
            if (mp.DRShieldConfig.MultDR > 0f)
                modifiers.FinalDamage *= (1f - mp.DRShieldConfig.MultDR);
        }
    }
}
