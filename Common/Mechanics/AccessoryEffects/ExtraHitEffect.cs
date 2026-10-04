using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Compatibility;

namespace TestMod.Common.Mechanics.AccessoryEffects
{
    // ============================================================================
    //  ExtraHitEffect  ——  额外伤害通用系统
    // ----------------------------------------------------------------------------
    //  用途：计算并打出一次"额外伤害"，供饰品、武器、盔甲套装调用。
    //  固定/属性部分吃一次完整玩家增伤；实际命中比例部分不再吃玩家增伤。
    //  Strike 与 SpawnProjectile 共用 Compute，分别负责直接打出和弹幕投送。
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
        // ── 伤害组成（固定/属性部分先增伤，再加实际命中比例部分）──────
        /// <summary>固定伤害部分。</summary>
        public float FlatDamage;

        /// <summary>玩家属性 × 此比例加入伤害（需配合 StatType）。</summary>
        public float PlayerStatRatio;

        /// <summary>参与计算的玩家属性，默认 None（PlayerStatRatio 不生效）。</summary>
        public PlayerStatType StatType;

        /// <summary>已结算的实际命中伤害 × 此比例；不再次应用玩家增伤。</summary>
        public float HitDamageRatio;

        // ── 伤害类型 ──────────────────────────────────────────────
        /// <summary>true = 跟随持握武器的 DamageClass；false = 使用 FixedClass。</summary>
        public bool FollowWeapon;

        /// <summary>FollowWeapon = false 时使用；null 时退化为 DamageClass.Generic。</summary>
        public DamageClass FixedClass;

        // ── 暴击 ──────────────────────────────────────────────────
        /// <summary>
        /// true = 使用玩家对应类型的完整暴击率独立判定，默认 false。
        /// 比例型/混合型效果保持 false，避免原命中暴击再次放大。
        /// </summary>
        public bool UseCrit;

        // ── 物理 ──────────────────────────────────────────────────
        /// <summary>击退力度，0 = 无击退。</summary>
        public float Knockback;

        /// <summary>额外无视防御。真实伤害类型也会由 NPC 通用钩子穿透防御；抗性仍正常结算。</summary>
        public bool IgnoreDefense;

        /// <summary>指定击退方向；null = 根据玩家与目标位置决定。</summary>
        public int? HitDirection;

        // ── 多人模式 ──────────────────────────────────────────────
        /// <summary>
        /// true = 不登记单机玩家击杀归属。不是 OnHit 回调开关，也不是同步开关。
        /// 直接 StrikeNPC 不分发物品/弹幕的玩家 OnHit 钩子；联机伤害包始终单独同步。
        /// </summary>
        public bool NoPlayerInteraction;

        // ── 战斗数字颜色 ──────────────────────────────────────────
        /// <summary>
        /// 自定义战斗数字颜色。null = 使用额外伤害默认紫色。
        /// 默认隐藏原版文字，改为显示动态浮字；UseVanillaCombatText 可保留原版数字。
        /// 注意：多人模式下自定义浮字仍以本地显示为主。
        /// </summary>
        public Color? CombatTextColor;

        /// <summary>
        /// 自定义动态战斗文字样式。留空时使用通用额外伤害样式。
        /// </summary>
        public string CombatTextStyleKey;

        /// <summary>直接打出时保留原版战斗数字，供既有溅射效果使用。</summary>
        public bool UseVanillaCombatText;
    }

    public static class ExtraHitEffect
    {
        public static readonly Color DefaultCombatTextColor = new(180, 50, 255);

        // ── 公开 API ──────────────────────────────────────────────────────────

        /// <summary>
        /// 仅计算伤害数值，不打出。
        /// 返回固定/属性部分的一次完整玩家增伤，加上未再次增伤的实际命中比例部分。
        /// 尚未结算目标防御、抗性或独立暴击；调用方不能再次乘玩家增伤。
        /// </summary>
        public static int Compute(Player player, in ExtraHitConfig cfg, int hitDamage = 0)
        {
            float dmg = cfg.FlatDamage;

            if (cfg.StatType != PlayerStatType.None && cfg.PlayerStatRatio != 0f)
                dmg += GetStatValue(player, cfg.StatType) * cfg.PlayerStatRatio;

            // 纯比例伤害不调用 ApplyTo(0)，避免玩家的平伤加成凭空加入这一部分。
            if (dmg > 0f)
                dmg = player.GetTotalDamage(ResolveDamageClass(player, cfg)).ApplyTo(dmg);

            dmg += hitDamage * cfg.HitDamageRatio;

            return Math.Max(1, (int)dmg);
        }

        /// <summary>
        /// 按统一规则计算，再通过 NPC 的防御/抗性路径直接打出。
        /// 不分发普通攻击的 OnHit 钩子，避免额外伤害再次触发追加攻击。
        /// 返回 NPC 实际损失的生命值（已扣除 NPC 防御）。
        /// </summary>
        public static int Strike(Player player, NPC target, in ExtraHitConfig cfg, int hitDamage = 0)
        {
            if (!target.active || target.life <= 0)
                return 0;

            DamageClass dmgClass   = ResolveDamageClass(player, cfg);
            int damage = Compute(player, cfg, hitDamage);
            int direction = cfg.HitDirection ?? (target.Center.X > player.Center.X ? 1 : -1);
            NPC.HitModifiers modifiers = target.GetIncomingStrikeModifiers(dmgClass, direction);
            ApplyHitRules(player, dmgClass, cfg.UseCrit, cfg.IgnoreDefense, ref modifiers);
            NPC.HitInfo hitInfo = modifiers.ToHitInfo(damage, false, cfg.Knockback);
            hitInfo.HideCombatText = !cfg.UseVanillaCombatText;
            int result = target.StrikeNPC(hitInfo, fromNet: false, cfg.NoPlayerInteraction);

            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendStrikeNPC(target, in hitInfo);

            // 仅在本地客户端显示自定义颜色的战斗数字
            if (!cfg.UseVanillaCombatText && Main.netMode != NetmodeID.Server)
                TextRenderingBridge.SpawnCombatText(
                    target.Hitbox,
                    hitInfo.Damage,
                    hitInfo.Crit,
                    cfg.CombatTextColor ?? DefaultCombatTextColor,
                    cfg.CombatTextStyleKey ?? TestModTextStyles.ExtraHit);

            return result;
        }

        /// <summary>既有比例溅射共用入口，保留其伤害类型、零击退方向和原版数字。</summary>
        public static int StrikeRatio(Player player, NPC target, int hitDamage, float ratio)
        {
            ExtraHitConfig cfg = new()
            {
                HitDamageRatio = ratio,
                FixedClass = DamageClass.Default,
                HitDirection = 0,
                UseVanillaCombatText = true,
            };
            return Strike(player, target, cfg, hitDamage);
        }

        /// <summary>
        /// 由 owner 端生成一次追加弹幕。伤害在生成时计算一次，暴击按命中时的玩家属性判定。
        /// 标记随弹幕同步；不继承父弹幕的暴击，也不允许再次触发本模组的追加攻击。
        /// </summary>
        public static int SpawnProjectile(Player player, IEntitySource source, Vector2 position, Vector2 velocity,
            int projectileType, in ExtraHitConfig cfg, int hitDamage = 0)
        {
            if (player.whoAmI != Main.myPlayer)
                return Main.maxProjectiles;

            int index = Projectile.NewProjectile(source, position, velocity, projectileType,
                Compute(player, cfg, hitDamage), cfg.Knockback, player.whoAmI);
            if (index < 0 || index >= Main.maxProjectiles)
                return index;

            Projectile projectile = Main.projectile[index];
            projectile.DamageType = ResolveDamageClass(player, cfg);
            projectile.CritChance = 0;
            var state = projectile.GetGlobalProjectile<global::TestMod.Common.GlobalProjectiles.GlobalProjectile>();
            state.IsExtraHit = true;
            state.ExtraHitUseCrit = cfg.UseCrit;
            state.ExtraHitIgnoreDefense = cfg.IgnoreDefense;
            projectile.netUpdate = true;
            return index;
        }

        public static bool IsExtraHitProjectile(Projectile projectile)
            => projectile.GetGlobalProjectile<global::TestMod.Common.GlobalProjectiles.GlobalProjectile>().IsExtraHit;

        /// <summary>直接伤害和追加弹幕共用暴击/防御规则；DisableCrit 优先于后续强制暴击。</summary>
        internal static void ApplyHitRules(Player player, DamageClass damageClass, bool useCrit, bool ignoreDefense,
            ref NPC.HitModifiers modifiers)
        {
            // 额外伤害已经按指定比例计算，不再叠加一次弹幕的随机伤害浮动。
            modifiers.DamageVariationScale *= 0f;

            if (useCrit && player.GetTotalCritChance(damageClass) > Main.rand.NextFloat() * 100f)
                modifiers.SetCrit();
            else
                modifiers.DisableCrit();

            if (ignoreDefense)
                modifiers.ScalingArmorPenetration += 1f;
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
                PlayerStatType.MeleeDamageMult  => player.GetTotalDamage(DamageClass.Melee).ApplyTo(1f),
                PlayerStatType.RangedDamageMult => player.GetTotalDamage(DamageClass.Ranged).ApplyTo(1f),
                PlayerStatType.MagicDamageMult  => player.GetTotalDamage(DamageClass.Magic).ApplyTo(1f),
                PlayerStatType.SummonDamageMult => player.GetTotalDamage(DamageClass.Summon).ApplyTo(1f),
                PlayerStatType.GenericDamageMult=> player.GetTotalDamage(DamageClass.Generic).ApplyTo(1f),
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
