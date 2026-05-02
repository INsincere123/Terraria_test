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
        public int   FlyTime;                // 翅膀飞行时间上限 (开 InfiniteFlight 后实际无限)
        public float HorizontalSpeed;        // 水平飞行速度
        public float HorizontalAccelMult;    // 水平飞行加速度倍率
        public float AscentWhenFalling;
        public float AscentWhenRising;
        public float MaxCanAscendMult;
        public float MaxAscentMult;
        public float ConstantAscend;

        public float HoverHorizontalSpeed;   // 悬浮时水平速度 (按下键, vanilla 提供的悬浮)
        public float HoverAccelMult;
        public float UpBoostMultiplier;      // 按上键加速倍率 (1.0f = 不加速)

        public bool  InfiniteFlight;         // empressBrooch 翱翔之证
        public bool  EnablePerfectHover;     // 完美悬浮 (按下+跳跃 视觉静止)
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
                flySpeedOverride: cfg.HorizontalSpeed,  // 留空时 vanilla 用 Player.accRunSpeed; 这里直接用水平速度
                hasHoldDownHoverFeatures: true,
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
