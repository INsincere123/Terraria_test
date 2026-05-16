using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Items.Accessories.Effects;

namespace TestMod.Items.Accessories
{
    // ============================================================================
    //  OmniWingItem  ——  翅膀类饰品抽象基类
    // ----------------------------------------------------------------------------
    //  为什么需要这个基类:
    //   - HorizontalWingSpeeds / VerticalWingSpeeds / SetStaticDefaults (WingStats)
    //     这些都是 ModItem 的 override 钩子, 不能从 Effects 静态方法调用。
    //   - 因此把"翅膀飞行参数 + vanilla 悬浮基础设施"打包到这个基类里,
    //     新翅膀只需继承并重写 Wings_Config 属性即可。
    //
    //  使用示例 (子类):
    //
    //      [AutoloadEquip(EquipType.Wings)]
    //      public class MyWing : OmniWingItem
    //      {
    //          protected override WingFlightConfig WingsConfig => new WingFlightConfig {
    //              FlyTime              = 3600,
    //              HorizontalSpeed      = 20f,
    //              HorizontalAccelMult  = 1.2f,
    //              AscentWhenFalling    = 1.2f,
    //              AscentWhenRising     = 0.1f,
    //              MaxCanAscendMult     = 0.8f,
    //              MaxAscentMult        = 3f,
    //              ConstantAscend       = 0.1f,
    //              HoverHorizontalSpeed = 7.5f,
    //              HoverAccelMult       = 1.5f,
    //              UpBoostMultiplier    = 5f,
    //              InfiniteFlight       = true,   // empressBrooch
    //              EnablePerfectHover   = true,
    //          };
    //
    //          public override void SetDefaults()
    //          {
    //              base.SetDefaults();      // 重要: 一定要调 base
    //              Item.value = ...
    //              Item.rare  = ...
    //          }
    //
    //          public override void UpdateAccessory(Player player, bool hideVisual)
    //          {
    //              base.UpdateAccessory(player, hideVisual);  // 应用翅膀飞行 + (可选) 完美悬浮
    //              // 此处自由调用 CombatStatsEffect.Apply / DefensiveStatsEffect.Apply / ...
    //          }
    //      }
    //
    //  [AutoloadEquip(EquipType.Wings)] 由子类自己加 (因为贴图按子类名字找)。
    // ============================================================================

    public struct WingFlightConfig
    {
        // ── 基础飞行 ─────────────────────────────────────────────────────────────

        // 翅膀飞行时间上限，单位 tick（60 tick = 1 秒）。
        // wingTime 每帧 -1，归零后无法继续飞行直到落地回满。
        // InfiniteFlight = true 时 wingTime 不再递减，此值无实际意义。
        // 参考：月光仙翼 1800 / 女皇之翼 1800 / 火神之翼 1500
        public int   FlyTime;

        // 飞行时的水平最大速度（覆写 vanilla 默认值 Player.accRunSpeed）。
        // 参考：幽灵翅膀 6.5 / 猪龙鱼翅膀 9 / 女皇之翼 9.5 / 天界星盘 8
        public float HorizontalSpeed;

        // 水平加速度倍率，乘在 vanilla 传入的 acceleration 上。
        // 1.0 = 不改变（默认加速度）；> 1 = 加速更快起步；0 = 永远无法加速到最高速（不要设 0）。
        public float HorizontalAccelMult;

        // 正在下坠（velocity.Y > 0）时按跳跃键的向上冲力。
        // 值越高越能"翻盘"下落，强行转为上升。
        // 参考：大多数 vanilla 翅膀 0.85 / 火神之翼 1.0
        public float AscentWhenFalling;

        // 已经在上升（velocity.Y < 0）时的额外持续向上加速量。
        // 仅在当前上升速度 < MaxAscentMult × 最大上升速 × MaxCanAscendMult 时生效。
        // 参考：大多数 vanilla 翅膀 0.1
        public float AscentWhenRising;

        // 控制 AscentWhenRising 何时停止生效的阈值比例。
        // 条件：currentUpSpeed / maxUpSpeed < MaxCanAscendMult 时才继续施加 AscentWhenRising。
        // 值越高 = 越接近极速时仍能加速（"顶满"效果更强）。
        // 参考：大多数 vanilla 翅膀 0.5
        public float MaxCanAscendMult;

        // 最大上升速度倍率，直接乘在 vanilla 内部计算的上升速度上限上。
        // 参考：大多数 vanilla 翅膀 1.5 / 喷气背包级别 3+
        public float MaxAscentMult;

        // 每帧恒定向上施加的微小推力，即使不按任何键也会生效。
        // 0 = 纯滑翔（靠 AscentWhenFalling 反向）；> 0 = 轻微上漂感。
        // 参考：大多数 vanilla 翅膀 0.1
        public float ConstantAscend;

        // ── 悬浮（需要 EnableHover = true）────────────────────────────────────

        // 是否启用按下键悬浮功能（vanilla WingStats.hasHoldDownHoverFeatures）。
        // false（默认）= 按下键不进入悬浮，HoverHorizontalSpeed / HoverAccelMult 无需填写。
        // true = 按住下键时进入悬浮模式，水平移动参数由下方两个字段控制。
        public bool  EnableHover;

        // 悬浮模式下的水平最大速度（仅 EnableHover = true 时有效）。
        // 参考：女皇之翼 6.25
        public float HoverHorizontalSpeed;

        // 悬浮模式下的水平加速度倍率（仅 EnableHover = true 时有效）。
        // 参考：女皇之翼 1.5 / 普通翅膀 1.0
        public float HoverAccelMult;

        // ── 特殊飞行行为 ──────────────────────────────────────────────────────

        // 按上键 + 跳跃键时，对 AscentWhenRising / MaxAscentMult / ConstantAscend 同乘此倍率。
        // 触发条件：> 1f；默认 0（struct 默认值）等同于不触发，无需显式写 1f。
        // 参考：女皇之翼 1.5 / 喷气背包级 5+
        public float UpBoostMultiplier;

        // 设为 true 时赋予 player.empressBrooch，飞行不消耗 wingTime（御翼徽章效果）。
        // bool 默认 false，不需要消耗飞行时间时可省略此字段。
        public bool  InfiniteFlight;

        // 设为 true 时启用完美悬浮：按下键 + 跳跃键时 velocity.Y 强制为 -0.0001f，
        // 视觉完全静止但 vanilla 仍判定为飞行中（防止"虚空跑步" bug）。
        // bool 默认 false，不需要时可省略。
        public bool  EnablePerfectHover;
    }

    public abstract class OmniWingItem : ModItem
    {
        protected abstract WingFlightConfig WingsConfig { get; }

        public override void SetStaticDefaults()
        {
            // 注册 vanilla 翅膀属性 —— 启用按下键悬浮的基础设施
            int wingSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Wings);
            var cfg = WingsConfig;

            ArmorIDs.Wing.Sets.Stats[wingSlot] = new WingStats(
                flyTime: cfg.FlyTime,
                flySpeedOverride: cfg.HorizontalSpeed,
                hasHoldDownHoverFeatures: cfg.EnableHover,
                hoverFlySpeedOverride: cfg.HoverHorizontalSpeed,
                hoverAccelerationMultiplier: cfg.HoverAccelMult
            );
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.accessory = true;
        }

        // ===== 翅膀飞行参数 (vanilla 钩子, 必须在 ModItem 上) =====

        public override void HorizontalWingSpeeds(Player player, ref float speed, ref float acceleration)
        {
            var cfg = WingsConfig;
            speed         = cfg.HorizontalSpeed;
            acceleration *= cfg.HorizontalAccelMult;
        }

        public override void VerticalWingSpeeds(Player player,
            ref float ascentWhenFalling,
            ref float ascentWhenRising,
            ref float maxCanAscendMultiplier,
            ref float maxAscentMultiplier,
            ref float constantAscend)
        {
            var cfg = WingsConfig;
            ascentWhenFalling      = cfg.AscentWhenFalling;
            ascentWhenRising       = cfg.AscentWhenRising;
            maxCanAscendMultiplier = cfg.MaxCanAscendMult;
            maxAscentMultiplier    = cfg.MaxAscentMult;
            constantAscend         = cfg.ConstantAscend;

            // 上升加速 (按上键 + 跳跃) —— 喷气背包 / 女皇之翼 风格
            if (cfg.UpBoostMultiplier > 1f && player.controlUp && player.controlJump)
            {
                ascentWhenRising    *= cfg.UpBoostMultiplier;
                maxAscentMultiplier *= cfg.UpBoostMultiplier;
                constantAscend      *= cfg.UpBoostMultiplier;
            }
        }

        // ===== 应用翅膀的"非飞行参数"部分 =====
        // 子类的 UpdateAccessory 在 base 调用之后可以自由叠加其它 Effect 模块

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var cfg = WingsConfig;

            if (cfg.InfiniteFlight)
                player.empressBrooch = true;

            if (cfg.EnablePerfectHover)
                PerfectHoverEffect.Apply(player);
        }
    }
}
