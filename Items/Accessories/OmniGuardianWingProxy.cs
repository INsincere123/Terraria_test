using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Items.Accessories
{
    // ============================================================================
    //  OmniGuardianWingProxy  ——  翅膀 slot 注册者（玩家永远不会获得此物品）
    // ----------------------------------------------------------------------------
    //  存在意义:
    //   - [AutoloadEquip(EquipType.Wings)] 注册翅膀 slot 和贴图
    //   - HorizontalWingSpeeds / VerticalWingSpeeds 绑定在此 ModItem 上
    //   - OmniGuardianAccessory（普通饰品格）在 UpdateAccessory 里设置标志，
    //     OmniEffectsPlayer.PostUpdateEquips 最后将 player.wingsLogic 指向此 slot，
    //     从而调用本类的飞行钩子，同时不与真实翅膀抢占翅膀格
    //
    //  贴图: OmniGuardianWingProxy_Wings.png（背部翅膀动画）
    //        物品图复用 OmniGuardianAccessory.png（此物品从不显示在物品栏）
    // ============================================================================

    [AutoloadEquip(EquipType.Wings)]
    internal class OmniGuardianWingProxy : ModItem
    {
        // 供外部读取已注册的 wing slot ID
        public static int WingSlot { get; private set; }

        // 复用主饰品的物品栏贴图，此物品本身永远不会被玩家持有
        public override string Texture => "TestMod/Items/Accessories/OmniGuardianAccessory";

        public override void SetStaticDefaults()
        {
            WingSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Wings);

            // 注册 vanilla 翅膀属性表——启用按下键悬浮的基础设施
            ArmorIDs.Wing.Sets.Stats[WingSlot] = new WingStats(
                flyTime:                     3600,
                flySpeedOverride:            20f,
                hasHoldDownHoverFeatures:    true,
                hoverFlySpeedOverride:       7.5f,
                hoverAccelerationMultiplier: 1.5f
            );
        }

        // ── 飞行参数（数值对齐 OmniGuardianAccessory.WingsConfig）─────────────
        public override void HorizontalWingSpeeds(Player player, ref float speed, ref float acceleration)
        {
            speed         = 20f;
            acceleration *= 1.2f;
        }

        public override void VerticalWingSpeeds(Player player,
            ref float ascentWhenFalling,
            ref float ascentWhenRising,
            ref float maxCanAscendMultiplier,
            ref float maxAscentMultiplier,
            ref float constantAscend)
        {
            ascentWhenFalling      = 1.2f;
            ascentWhenRising       = 0.1f;
            maxCanAscendMultiplier = 0.8f;
            maxAscentMultiplier    = 3f;
            constantAscend         = 0.1f;

            // 按上键 + 跳跃：喷气背包级上冲（×5 倍）
            if (player.controlUp && player.controlJump)
            {
                ascentWhenRising    *= 5f;
                maxAscentMultiplier *= 5f;
                constantAscend      *= 5f;
            }
        }
        // ─────────────────────────────────────────────────────────────────────

        public override void SetDefaults()
        {
            Item.width    = 2;
            Item.height   = 2;
            Item.maxStack = 1;
            Item.rare     = ItemRarityID.White;
            Item.accessory = true;
        }

        // 禁止出现在任何战利品池 / 商店 / 合成
        public override void AddRecipes() { }
    }
}
