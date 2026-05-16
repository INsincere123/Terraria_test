using Terraria;
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
    internal class OmniGuardianWingProxy : OmniWingItem
    {
        // 供外部读取已注册的 wing slot ID
        public static int WingSlot { get; private set; }

        // 复用主饰品的物品栏贴图，此物品本身永远不会被玩家持有
        public override string Texture => "TestMod/Items/Accessories/OmniGuardianAccessory";

        // ── 数值调节区 ────────────────────────────────────────────────
        protected override WingFlightConfig WingsConfig => new WingFlightConfig {
            FlyTime              = 3600,
            HorizontalSpeed      = 20f,
            HorizontalAccelMult  = 1.2f,
            AscentWhenFalling    = 1.2f,
            AscentWhenRising     = 0.1f,
            MaxCanAscendMult     = 0.8f,
            MaxAscentMult        = 3f,
            ConstantAscend       = 0.1f,
            EnableHover          = true,
            HoverHorizontalSpeed = 7.5f,
            HoverAccelMult       = 1.5f,
            UpBoostMultiplier    = 5f,
            // InfiniteFlight / EnablePerfectHover 由 OmniGuardianAccessory 负责，
            // 此 proxy 永远不被装备，UpdateAccessory 不会被调用
        };
        // ─────────────────────────────────────────────────────────────

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults(); // 读 WingsConfig 填入 WingStats
            WingSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Wings);
        }

        public override void SetDefaults()
        {
            // 不调 base：OmniWingItem.SetDefaults 设 width/height=30，
            // proxy 永不显示在物品栏，用最小尺寸即可
            Item.width     = 2;
            Item.height    = 2;
            Item.maxStack  = 1;
            Item.rare      = ItemRarityID.White;
            Item.accessory = true;
        }
    }
}
