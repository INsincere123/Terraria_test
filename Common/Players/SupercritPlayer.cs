using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TestMod.Buffs;
using TestMod.Common.DynamicText;

namespace TestMod.Common.Players
{
    /// <summary>
    /// 超暴击机制的玩家数据载体。
    /// supercritEnabled 由 SupercritBuff.Update 每帧设置，ResetEffects 每帧清零。
    /// </summary>
    public class SupercritPlayer : ModPlayer
    {
        public bool supercritEnabled;

        public override void ResetEffects()
        {
            supercritEnabled = false;
        }
    }

    /// <summary>
    /// 在弹幕命中 NPC 时实施超暴击逻辑：
    /// 当玩家暴击率超过 100% 时，溢出部分按 1% 暴击率 → 2% 暴击伤害 的比例
    /// 加到本次命中的 modifiers.CritDamage 上。
    ///
    /// 由于暴击率 ≥ 100% 时必然暴击，CritDamage 必然生效，不需要任何条件判断。
    /// </summary>
    public class SupercritGlobalNPC : GlobalNPC
    {
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead)
                return;

            SupercritPlayer modPlayer = player.GetModPlayer<SupercritPlayer>();
            if (!modPlayer.supercritEnabled)
                return;

            // 按弹幕的伤害类读取玩家当前暴击率，得到溢出 100% 的部分
            float critOver100 = player.GetTotalCritChance(projectile.DamageType) - 100f;
            if (critOver100 <= 0f)
                return;

            // 1% 溢出暴击率 → 2% 暴击伤害（系数从 SupercritBuff 取，方便统一调整）
            // CritDamage 是 StatModifier，+= float 走加法偏移；
            // 基础值 1.0 表示暴击 ×2.0，每 +1.0 等于多 100% 暴击伤害（×3.0、×4.0 …）
            // 因此这里需要把"百分比"换算成"小数倍率"：bonus% / 100
            float bonusCritDamage = critOver100 * SupercritBuff.CritOverflowToCritDamageRatio / 100f;
            modifiers.CritDamage += bonusCritDamage;
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            if (!hit.Crit || projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active || player.dead || !player.GetModPlayer<SupercritPlayer>().supercritEnabled)
                return;

            float critOver100 = player.GetTotalCritChance(projectile.DamageType) - 100f;
            if (critOver100 <= 0f)
                return;

            int bonusPercent = (int)MathF.Round(critOver100 * SupercritBuff.CritOverflowToCritDamageRatio);
            DynamicWorldTextSystem.Spawn(new DynamicWorldTextRequest(
                $"+{bonusPercent}%暴伤",
                npc.Center + Vector2.UnitY * 8f,
                DynamicTextStyleRegistry.Supercrit,
                new Color(255, 214, 62),
                crit: true,
                scale: 0.78f,
                seed: projectile.identity * 31 + npc.whoAmI * 197 + (int)Main.GameUpdateCount));
        }
    }
}
