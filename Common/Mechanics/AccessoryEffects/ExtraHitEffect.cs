using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.DynamicText;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    // ============================================================================
    //  ExtraHitEffect  ——  额外伤害通用系统
    // ----------------------------------------------------------------------------
    //  用途：计算并打出一次"额外伤害"，供饰品、武器、盔甲套装调用。
    //  支持直接打出（SimpleStrikeNPC）或仅计算数值（供弹幕/投射物使用）。
    //
    //  使用示例（直接打出）：
    //    int dealt = ExtraHitEffect.Strike(player, target, new ExtraHitConfig
    //    {
    //        FlatDamage      = 240f,
    //        PlayerStatRatio = 0.88f,
    //        StatType        = PlayerStatType.MaxHP,
    //        FollowWeapon    = true,
    //        UseCrit         = true,
    //    });
    //
    //  使用示例（仅计算，供弹幕自己投送）：
    //    int damage = ExtraHitEffect.Compute(player, new ExtraHitConfig
    //    {
    //        HitDamageRatio = 0.19f,
    //    }, damageDone);
    // ============================================================================

    /// <summary>
    /// 可参与伤害计算的玩家属性列表。
    /// GetStatValue() 负责把枚举转换为具体数值。
    /// </summary>
    public enum PlayerStatType
    {
        None = 0,

        // ── 生命 ──────────────────────────────────────────────────
        MaxHP,          // player.statLifeMax2（最大生命值）
        CurrentHP,      // player.statLife（当前生命值）
        MissingHP,      // statLifeMax2 - statLife（已损失生命值）

        // ── 魔力 ──────────────────────────────────────────────────
        MaxMana,        // player.statManaMax2
        CurrentMana,    // player.statMana
        MissingMana,    // statManaMax2 - statMana

        // ── 防御 ──────────────────────────────────────────────────
        Defense,        // player.statDefense

        // ── 伤害倍率（ApplyTo(1f) = 综合倍率，含加法/乘法加成）──────
        MeleeDamageMult,
        RangedDamageMult,
        MagicDamageMult,
        SummonDamageMult,
        GenericDamageMult,

        // ── 移动 ──────────────────────────────────────────────────
        MoveSpeed,      // player.moveSpeed（1.0 = 原速）

        // ── 召唤 ──────────────────────────────────────────────────
        MaxMinions,     // player.maxMinions

        // ── 幸运 ──────────────────────────────────────────────────
        Luck,           // player.luck

        // ── 武器 ──────────────────────────────────────────────────
        HeldItemDamage, // player.HeldItem.damage（面板基础伤害，不含玩家加成）
    }

    /// <summary>额外伤害配置，所有字段默认值均等于"不生效"（0 / false / None）。</summary>
    public struct ExtraHitConfig
    {
        // ── 伤害组成（三项相加为最终基础伤害）────────────────────────
        /// <summary>固定伤害部分。</summary>
        public float FlatDamage;

        /// <summary>玩家属性 × 此比例加入伤害（需配合 StatType）。</summary>
        public float PlayerStatRatio;

        /// <summary>参与计算的玩家属性，默认 None（PlayerStatRatio 不生效）。</summary>
        public PlayerStatType StatType;

        /// <summary>触发伤害 × 此比例加入伤害（传入 hitDamage 时生效）。</summary>
        public float HitDamageRatio;

        // ── 伤害类型 ──────────────────────────────────────────────
        /// <summary>true = 跟随持握武器的 DamageClass；false = 使用 FixedClass。</summary>
        public bool FollowWeapon;

        /// <summary>FollowWeapon = false 时使用；null 时退化为 DamageClass.Generic。</summary>
        public DamageClass FixedClass;

        // ── 暴击 ──────────────────────────────────────────────────
        /// <summary>true = 使用玩家对应伤害类型的暴击率独立判定。</summary>
        public bool UseCrit;

        // ── 物理 ──────────────────────────────────────────────────
        /// <summary>击退力度，0 = 无击退。</summary>
        public float Knockback;

        // ── 多人模式 ──────────────────────────────────────────────
        /// <summary>
        /// true = 不触发 OnHit 系列玩家钩子，适合防递归场景。
        /// false = 完整触发所有钩子并自动同步网络包（推荐默认值）。
        /// </summary>
        public bool NoPlayerInteraction;

        // ── 战斗数字颜色 ──────────────────────────────────────────
        /// <summary>
        /// 自定义战斗数字颜色。null = 使用额外伤害默认紫色。
        /// 始终隐藏原版文字，改为显示动态浮字。
        /// 注意：多人模式下自定义浮字仍以本地显示为主。
        /// </summary>
        public Color? CombatTextColor;

        /// <summary>
        /// 自定义动态战斗文字样式。留空时使用通用额外伤害样式。
        /// </summary>
        public string CombatTextStyleKey;
    }

    public static class ExtraHitEffect
    {
        public static readonly Color DefaultCombatTextColor = new(180, 50, 255);

        // ── 公开 API ──────────────────────────────────────────────────────────

        /// <summary>
        /// 仅计算伤害数值，不打出。
        /// 返回值为基础伤害（FlatDamage + 属性比例 + 触发伤害比例），
        /// 不含玩家伤害加成（由调用方或弹幕自行应用）。
        /// </summary>
        public static int Compute(Player player, in ExtraHitConfig cfg, int hitDamage = 0)
        {
            float dmg = cfg.FlatDamage;

            if (cfg.StatType != PlayerStatType.None && cfg.PlayerStatRatio != 0f)
                dmg += GetStatValue(player, cfg.StatType) * cfg.PlayerStatRatio;

            if (cfg.HitDamageRatio != 0f)
                dmg += hitDamage * cfg.HitDamageRatio;

            return Math.Max(1, (int)dmg);
        }

        /// <summary>
        /// 计算伤害并通过 SimpleStrikeNPC 直接打出。
        /// 会自动应用玩家伤害加成与暴击判定。
        /// 返回 NPC 实际损失的生命值（已扣除 NPC 防御）。
        /// </summary>
        public static int Strike(Player player, NPC target, in ExtraHitConfig cfg, int hitDamage = 0)
        {
            DamageClass dmgClass   = ResolveDamageClass(player, cfg);
            int         baseDamage = Compute(player, cfg, hitDamage);
            int  scaledDamage = (int)player.GetDamage(dmgClass).ApplyTo(baseDamage);
            bool crit         = cfg.UseCrit && player.GetCritChance(dmgClass) > Main.rand.NextFloat() * 100f;
            int  direction    = target.Center.X > player.Center.X ? 1 : -1;

            // 统一隐藏原版数字，改走动态浮字。颜色仍可由配置覆盖。
            NPC.HitInfo hitInfo        = target.CalculateHitInfo(scaledDamage, direction, crit, cfg.Knockback, dmgClass);
            hitInfo.HideCombatText     = true;
            int result = target.StrikeNPC(hitInfo, fromNet: false, cfg.NoPlayerInteraction);

            if (!cfg.NoPlayerInteraction)
                NetMessage.SendStrikeNPC(target, in hitInfo);  // 同步伤害包（含 HideCombatText=true）

            // 仅在本地客户端显示自定义颜色的战斗数字
            if (Main.netMode != NetmodeID.Server)
                DynamicWorldTextSystem.SpawnCombatText(
                    target.Hitbox,
                    hitInfo.Damage,
                    hitInfo.Crit,
                    cfg.CombatTextColor ?? DefaultCombatTextColor,
                    cfg.CombatTextStyleKey ?? DynamicTextStyleRegistry.ExtraHit);

            return result;
        }

        /// <summary>将 PlayerStatType 枚举转换为对应的玩家属性数值。</summary>
        public static float GetStatValue(Player player, PlayerStatType type)
        {
            return type switch
            {
                PlayerStatType.None             => 0f,
                PlayerStatType.MaxHP            => player.statLifeMax2,
                PlayerStatType.CurrentHP        => player.statLife,
                PlayerStatType.MissingHP        => player.statLifeMax2 - player.statLife,
                PlayerStatType.MaxMana          => player.statManaMax2,
                PlayerStatType.CurrentMana      => player.statMana,
                PlayerStatType.MissingMana      => player.statManaMax2 - player.statMana,
                PlayerStatType.Defense          => player.statDefense,
                PlayerStatType.MeleeDamageMult  => player.GetDamage(DamageClass.Melee).ApplyTo(1f),
                PlayerStatType.RangedDamageMult => player.GetDamage(DamageClass.Ranged).ApplyTo(1f),
                PlayerStatType.MagicDamageMult  => player.GetDamage(DamageClass.Magic).ApplyTo(1f),
                PlayerStatType.SummonDamageMult => player.GetDamage(DamageClass.Summon).ApplyTo(1f),
                PlayerStatType.GenericDamageMult=> player.GetDamage(DamageClass.Generic).ApplyTo(1f),
                PlayerStatType.MoveSpeed        => player.moveSpeed,
                PlayerStatType.MaxMinions       => player.maxMinions,
                PlayerStatType.Luck             => player.luck,
                PlayerStatType.HeldItemDamage   => player.HeldItem is { IsAir: false } h ? h.damage : 0f,
                _                               => 0f
            };
        }

        // ── 内部工具 ──────────────────────────────────────────────────────────

        private static DamageClass ResolveDamageClass(Player player, in ExtraHitConfig cfg)
        {
            if (!cfg.FollowWeapon)
                return cfg.FixedClass ?? DamageClass.Generic;

            Item held = player.HeldItem;
            return (held == null || held.IsAir || held.damage <= 0)
                ? DamageClass.Melee
                : held.DamageType ?? DamageClass.Melee;
        }
    }
}
