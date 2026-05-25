using System;
using Terraria;

namespace TestMod.Content.Items.Accessories.Effects
{
    // ============================================================================
    //  SurvivalEffect  ——  综合生存效果 (Radiance + RampartOfDeities 风格)
    // ----------------------------------------------------------------------------
    //  包含 5 个独立的子效果, 每个可单独开关:
    //   [1] 低血量额外免伤      —— HP < 50% 时 endurance 增加
    //   [2] Debuff 堆叠加成     —— 身上每个 debuff 增加防御 + 再生
    //   [3] 失血再生            —— 失血越多, 再生越高 (Min~Max 线性插值)
    //   [4] Debuff 加速衰减     —— 每帧多减少 N tick 的 debuff 时间
    //   [5] 额外受伤无敌帧      —— 受伤后追加无敌时间
    //
    //  使用方式 (在饰品 UpdateAccessory 中):
    //
    //    SurvivalEffect.Apply(player, new SurvivalConfig {
    //        EnableLowHpReduction  = true,
    //        LowHpDamageReduction  = 0.35f,
    //        EnableDebuffStack     = true,
    //        DebuffDefensePerStack = 20,
    //        DebuffRegenPerStack   = 10,
    //        EnableLostHpRegen     = true,
    //        LostHpRegenMin        = 5,
    //        LostHpRegenMax        = 50,
    //        EnableDebuffDecay     = true,
    //        DebuffTimeReduction   = 2,
    //        EnableExtraImmuneFrames = true,
    //        ExtraImmuneFrames     = 10,
    //    });
    //
    //  Apply() 仅设置 OmniEffectsPlayer 上的"启用标志"和参数,
    //  真正的逻辑在 UpdateLifeRegen / UpdateMiscEffects 钩子分发时执行。
    // ============================================================================

    public struct SurvivalConfig
    {
        // [1] 低血量额外免伤
        public bool EnableLowHpReduction;
        public float LowHpDamageReduction;   // 0.35f = +35% 免伤 (HP < 50% 时)

        // [2] Debuff 堆叠加防御 / 再生
        public bool EnableDebuffStack;
        public int DebuffDefensePerStack;
        public int DebuffRegenPerStack;     // 单位是 1/2 HP/s (vanilla lifeRegen 原生单位)

        // [3] 失血再生 (HP 越低再生越高)
        public bool EnableLostHpRegen;
        public int LostHpRegenMin;          // 单位 HP/s
        public int LostHpRegenMax;

        // [4] Debuff 时间加速衰减
        public bool EnableDebuffDecay;
        public int DebuffTimeReduction;     // 每帧多减的 tick

        // [5] 受伤额外无敌帧
        public bool EnableExtraImmuneFrames;
        public int ExtraImmuneFrames;
    }

    public static class SurvivalEffect
    {
        public static void Apply(Player player, SurvivalConfig cfg)
        {
            var mp = player.GetModPlayer<OmniEffectsPlayer>();

            if (cfg.EnableLowHpReduction)
            {
                mp.EnableSurvivalLowHp = true;
                mp.Survival_LowHpDamageReduction = cfg.LowHpDamageReduction;
            }

            if (cfg.EnableDebuffStack)
            {
                mp.EnableSurvivalDebuffStack = true;
                mp.Survival_DebuffDefensePerStack = cfg.DebuffDefensePerStack;
                mp.Survival_DebuffRegenPerStack = cfg.DebuffRegenPerStack;
            }

            if (cfg.EnableLostHpRegen)
            {
                mp.EnableSurvivalRegen = true;
                mp.Survival_LostHpRegenMin = cfg.LostHpRegenMin;
                mp.Survival_LostHpRegenMax = cfg.LostHpRegenMax;
            }

            if (cfg.EnableDebuffDecay)
            {
                mp.EnableSurvivalDebuffDecay = true;
                mp.Survival_DebuffTimeReduction = cfg.DebuffTimeReduction;
            }

            if (cfg.EnableExtraImmuneFrames)
            {
                mp.EnableSurvivalImmuneFrames = true;
                mp.Survival_ExtraImmuneFrames = cfg.ExtraImmuneFrames;
            }
        }

        // =====================================================================
        // 由 OmniEffectsPlayer.UpdateLifeRegen 调用
        // —— 失血再生
        // =====================================================================
        public static void UpdateLifeRegen(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnableSurvivalRegen) return;

            float lostFraction = 1f - ((float)player.statLife / Math.Max(1, player.statLifeMax2));
            if (lostFraction < 0f) lostFraction = 0f;
            if (lostFraction > 1f) lostFraction = 1f;

            int minR = mp.Survival_LostHpRegenMin;
            int maxR = mp.Survival_LostHpRegenMax;
            int bonus = minR + (int)Math.Round(lostFraction * (maxR - minR));

            player.lifeRegen += bonus * 2; // lifeRegen 单位是 1/2 HP/s
        }

        // =====================================================================
        // 由 OmniEffectsPlayer.PostUpdateMiscEffects 调用
        // —— 处理其它 4 个子效果
        // =====================================================================
        public static void UpdateMiscEffects(Player player, OmniEffectsPlayer mp)
        {
            // ===== Debuff 计数 + 加速衰减 =====
            int debuffCount = 0;
            if (mp.EnableSurvivalDebuffStack || mp.EnableSurvivalDebuffDecay)
            {
                for (int i = 0; i < Player.MaxBuffs; i++)
                {
                    int bt = player.buffType[i];
                    if (bt <= 0) continue;
                    if (Main.debuff[bt])
                    {
                        debuffCount++;
                        if (mp.EnableSurvivalDebuffDecay)
                        {
                            player.buffTime[i] -= mp.Survival_DebuffTimeReduction;
                            if (player.buffTime[i] < 1) player.buffTime[i] = 1;
                        }
                    }
                }
            }

            if (mp.EnableSurvivalDebuffStack && debuffCount > 0)
            {
                player.statDefense += debuffCount * mp.Survival_DebuffDefensePerStack;
                player.lifeRegen += debuffCount * mp.Survival_DebuffRegenPerStack;
            }

            // ===== 低血量额外免伤 =====
            if (mp.EnableSurvivalLowHp && player.statLife < player.statLifeMax2 / 2)
            {
                player.endurance += mp.Survival_LowHpDamageReduction;
            }

            // ===== 额外受伤无敌帧 =====
            // 已移至 OnHurt，见 SurvivalEffect.OnHurt()
        }
        // =====================================================================
        // 由 OmniEffectsPlayer.OnHurt 调用
        // —— 受伤瞬间追加无敌帧（只触发一次，不会每帧续期）
        // =====================================================================
        public static void OnHurt(Player player, OmniEffectsPlayer mp)
        {
            if (!mp.EnableSurvivalImmuneFrames || mp.Survival_ExtraImmuneFrames <= 0) return;

            player.SetImmuneTimeForAllTypes(player.immuneTime + mp.Survival_ExtraImmuneFrames);
        }
    }
}
