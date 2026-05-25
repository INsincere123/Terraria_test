using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.Mechanics.AccessoryEffects
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
        public bool GrantOmniWings;            // OmniGuardianAccessory 已装备 → PostUpdateEquips 覆写 wingsLogic
        public bool EnableFastFall;            // 启用快速下落
        public bool EnableTripleDodgeExtra;    // 启用自定义额外闪避 (神圣套/黑带闪避不需要标志, vanilla 自动管理)
        public bool EnablePerfectHover;        // 启用完美悬浮
        public bool EnableSurvivalRegen;       // 启用失血再生
        public bool EnableSurvivalDebuffStack; // 启用 debuff 堆叠加防御/再生
        public bool EnableSurvivalLowHp;       // 启用低血量额外免伤
        public bool EnableSurvivalDebuffDecay; // 启用 debuff 时间加速衰减
        public bool EnableSurvivalImmuneFrames;// 启用额外无敌帧
        public bool EnableGrapple;             // 启用红木魔石钩爪效果
        public bool EnableDRShield;            // 启用伤害减免护盾
        public bool EnableReflectShield;       // 启用反射护盾
        public bool EnablePlayerSize;          // 启用体型缩放
        public bool EnableLifesteal;           // 启用吸血

        // ===== 内部计时器 / 持久状态 =====
        public int ExtraDodgeCooldown;        // 自定义额外闪避的冷却 tick
        public bool GrappleDRActive;          // 钩爪伤害减免是否激活（钩中或松钩后1秒内）
        public int  GrappleDRTimer;           // 松钩后倒计时（60 = 1秒）
        // DRShield 持久状态（冷却即使卸装备也继续倒数）
        public float DRShieldRotationAngle;
        public int   DRShieldRespawnCooldown;
        // ReflectShield 持久状态
        public float ReflectShieldRotationAngle;
        public int   ReflectShieldRespawnCooldown;

        // ===== 护盾配置参数（每帧由 Apply 写入）=====
        public DRShieldConfig      DRShieldConfig;
        public ReflectShieldConfig ReflectShieldConfig;

        // ===== 体型缩放配置（每帧由 Apply 写入）=====
        public PlayerSizeConfig PlayerSizeConfig;
        public float            PlayerSizeCurrentScale; // PostUpdate 计算后供 TransformDrawData 读取

        // ===== 吸血配置（每帧由 Apply 写入）=====
        public LifestealConfig LifestealConfig;
        public int             LifestealCooldown;      // 两次触发间冷却（即使卸下装备也继续倒数）
        public int             LifestealPendingRegen;  // LifeRegen 模式的挂起治疗量（平滑释放）

        // ===== 强制暴击/追踪状态（由 ForcedCritEffect.Apply 写入，按命中次数消耗）=====
        // 不在 ResetEffects 中重置——这些是跨帧持久计数，仅在激活时覆盖
        public int            ForcedCritRemaining;    // 剩余强制暴击次数
        public int            ForcedHomingRemaining;  // 剩余强制追踪弹幕次数（由弹幕 OnSpawn 消耗）
        public float          ForcedCritBoost;        // 施加的暴击率加成（用于还原自然暴击率）
        public ExtraHitConfig ForcedCritExtraHit;     // 本该暴击时触发的额外伤害配置

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
            GrantOmniWings = false;
            EnableFastFall = false;
            EnableTripleDodgeExtra = false;
            EnablePerfectHover = false;
            EnableSurvivalRegen = false;
            EnableSurvivalDebuffStack = false;
            EnableSurvivalLowHp = false;
            EnableSurvivalDebuffDecay = false;
            EnableSurvivalImmuneFrames = false;
            EnableGrapple        = false;
            EnableDRShield       = false;
            EnableReflectShield  = false;
            EnablePlayerSize     = false;
            PlayerSizeCurrentScale = 1f;
            EnableLifesteal      = false;

            // 药水加成每帧重置 (脱装备后立即失效)
            Potion_HealFlatBonus = 0;
            Potion_HealMultBonus = 0f;
        }

        // 冷却需要持续衰减 (即使没装备饰品也衰减, 否则脱装备就锁死)
        public override void PostUpdate()
        {
            if (ExtraDodgeCooldown > 0)
                ExtraDodgeCooldown--;
            if (DRShieldRespawnCooldown > 0)
                DRShieldRespawnCooldown--;
            if (ReflectShieldRespawnCooldown > 0)
                ReflectShieldRespawnCooldown--;

            // 卸下装备时还原 width/height（width 不会被原版每帧重置，需手动归位）
            PlayerSizeEffect.RestoreHitbox(Player, this);

            // 吸血冷却持续倒数（脱装备后仍继续，保证重新穿上时不立即触发）
            if (LifestealCooldown > 0) LifestealCooldown--;
        }

        // 所有绘制层填充完毕后缩放视觉，与碰撞箱 scale 保持一致
        public override void TransformDrawData(ref PlayerDrawSet drawInfo)
        {
            if (EnablePlayerSize)
                PlayerSizeEffect.ApplyDrawScale(ref drawInfo, PlayerSizeCurrentScale);
        }

        // ===== 钩子分发 ===================================================

        public override void PostUpdateEquips()
        {
            // 强制暴击：有剩余计数时施加暴击率加成，使下一次命中必然暴击
            if (ForcedCritRemaining > 0)
                Player.GetCritChance(DamageClass.Generic) += ForcedCritBoost;

            // 翅膀覆写：在所有 UpdateAccessory 结束后最后写入，确保胜过同帧已装备的真实翅膀
            if (GrantOmniWings)
            {
                int slot = OmniGuardianWingProxy.WingSlot;
                Player.wings      = slot;   // 视觉：显示 Proxy 的翅膀贴图和动画
                Player.wingsLogic = slot;   // 物理：调用 Proxy 的 HorizontalWingSpeeds / VerticalWingSpeeds
                Player.empressBrooch  = true;  // 飞行不消耗时间
                Player.wingTimeMax    = 3600;  // 备用（empressBrooch 已开，实际不消耗）
            }
        }

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
            LifestealEffect.DrainPendingRegen(Player, this);
        }

        public override void PostUpdateMiscEffects()
        {
            SurvivalEffect.UpdateMiscEffects(Player, this);
            GrappleEffect.UpdateMiscEffects(Player, this);
            DRShieldEffect.UpdateMiscEffects(Player, this);
            ReflectShieldEffect.UpdateMiscEffects(Player, this);
            // 体型缩放：在 TileCollision 之后、物理结算完毕后修改碰撞箱（灾厄模式）
            PlayerSizeEffect.UpdateHitbox(Player, this);
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            LifestealEffect.TryHeal(Player, this, hit, damageDone, fromProjectile: false);

            if (ForcedCritRemaining > 0)
            {
                ForcedCritRemaining--;
                TryForcedCritExtraHit(item.DamageType, target, damageDone);
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            LifestealEffect.TryHeal(Player, this, hit, damageDone, fromProjectile: true);

            if (ForcedCritRemaining > 0)
            {
                ForcedCritRemaining--;
                TryForcedCritExtraHit(proj.DamageType, target, damageDone);
            }
        }

        // 独立判定"本该暴击"概率，满足时触发额外伤害
        private void TryForcedCritExtraHit(DamageClass dmgType, NPC target, int damageDone)
        {
            // GetCritChance 只返回该职业自身的 stat，不含父类。
            // 有效暴击率 = GetCritChance(dmgType) + GetCritChance(Generic)（父类继承叠加）
            // 自然暴击率 = 有效暴击率 - ForcedCritBoost（减去我们加在 Generic 上的强制量）
            float naturalCrit = MathF.Max(0f,
                Player.GetCritChance(dmgType) + Player.GetCritChance(DamageClass.Generic) - ForcedCritBoost);

            // 自然暴击率超 100% 时 rand*100 始终 <= naturalCrit，触发概率 100%（正确行为）
            if (naturalCrit <= Main.rand.NextFloat() * 100f) return;

            ExtraHitEffect.Strike(Player, target, ForcedCritExtraHit, damageDone);
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
