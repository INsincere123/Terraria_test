using Terraria;
using Terraria.ModLoader;

namespace TestMod.Items.Accessories.Effects
{
    // ============================================================================
    //  OmniEffectsPlayer  ——  Effects 模块共享的 ModPlayer 状态承载
    // ----------------------------------------------------------------------------
    //  作用:
    //   - 承载所有需要"持续状态"的效果模块的字段 (闪避冷却、快速下落开关等)
    //   - 饰品每帧通过设置 EnableXxx = true 来"启用"对应效果
    //   - 该 ModPlayer 在 ResetEffects 中重置所有 Enable 标志
    //   - 真正的逻辑分发到各 Effect 模块的静态方法 Update*
    //
    //  这是和 vanilla "accFlipper / accBoots" 等设计一致的模式: 每帧重置 + 装备时置 true
    // ============================================================================

    public class OmniEffectsPlayer : ModPlayer
    {
        // ===== 启用标志 (饰品每帧重新设置) =====
        public bool EnableFastFall;            // 启用快速下落
        public bool EnableTripleDodgeExtra;    // 启用自定义额外闪避 (神圣套/黑带闪避不需要标志, vanilla 自动管理)
        public bool EnablePerfectHover;        // 启用完美悬浮
        public bool EnableSurvivalRegen;       // 启用失血再生
        public bool EnableSurvivalDebuffStack; // 启用 debuff 堆叠加防御/再生
        public bool EnableSurvivalLowHp;       // 启用低血量额外免伤
        public bool EnableSurvivalDebuffDecay; // 启用 debuff 时间加速衰减
        public bool EnableSurvivalImmuneFrames;// 启用额外无敌帧

        // ===== 内部计时器 / 持久状态 =====
        public int ExtraDodgeCooldown;        // 自定义额外闪避的冷却 tick

        // ===== 配置参数 (由饰品在 UpdateAccessory 时写入, 让模块知道用什么数值) =====
        public float FastFall_MaxFallSpeed;
        public float FastFall_GravityBoost;

        public int ExtraDodge_ChanceDenominator;
        public int ExtraDodge_CooldownTicks;

        public int Survival_LostHpRegenMin;
        public int Survival_LostHpRegenMax;
        public int Survival_DebuffDefensePerStack;
        public int Survival_DebuffRegenPerStack;
        public int Survival_DebuffTimeReduction;
        public float Survival_LowHpDamageReduction;
        public int Survival_ExtraImmuneFrames;

        public int Potion_HealFlatBonus;
        public float Potion_HealMultBonus;

        public override void ResetEffects()
        {
            EnableFastFall = false;
            EnableTripleDodgeExtra = false;
            EnablePerfectHover = false;
            EnableSurvivalRegen = false;
            EnableSurvivalDebuffStack = false;
            EnableSurvivalLowHp = false;
            EnableSurvivalDebuffDecay = false;
            EnableSurvivalImmuneFrames = false;

            // 药水加成每帧重置 (脱装备后立即失效)
            Potion_HealFlatBonus = 0;
            Potion_HealMultBonus = 0f;
        }

        // 冷却需要持续衰减 (即使没装备饰品也衰减, 否则脱装备就锁死)
        public override void PostUpdate()
        {
            if (ExtraDodgeCooldown > 0)
                ExtraDodgeCooldown--;
        }

        // ===== 钩子分发 ===================================================

        public override void PostUpdateRunSpeeds()
        {
            FastFallEffect.UpdateRunSpeeds(Player, this);
        }

        public override void PreUpdateMovement()
        {
            PerfectHoverEffect.UpdateMovement(Player, this);
        }

        public override void UpdateLifeRegen()
        {
            SurvivalEffect.UpdateLifeRegen(Player, this);
        }

        public override void PostUpdateMiscEffects()
        {
            SurvivalEffect.UpdateMiscEffects(Player, this);
        }

        public override bool FreeDodge(Player.HurtInfo info)
        {
            return TripleDodgeEffect.TryExtraDodge(Player, this);
        }

        public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
        {
            PotionEffect.OnGetHealLife(this, ref healValue);
        }

        public override void PostHurt(Player.HurtInfo info)
        {
            SurvivalEffect.OnHurt(Player, this);
        }
    }
}
