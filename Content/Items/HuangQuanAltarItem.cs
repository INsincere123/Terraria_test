using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Tiles;

namespace TestMod.Content.Items
{
    /// <summary>
    /// 黄泉馈灵塔 — 可放置物品，放置后生成 HuangQuanAltarTile。
    ///
    /// ⚠️ 贴图占位说明：
    ///   当前 Texture 指向原版 Tombstone 物品贴图（仅供开发期测试）。
    ///   正式使用时，请在 Content/Items/HuangQuanAltarItem.png 提供自定义贴图并删除此 override。
    /// </summary>
    public class HuangQuanAltarItem : ModItem
    {
        // 开发期占位贴图（Tombstone 物品）；替换为自定义 PNG 后删除此 override
        public override string Texture => "Terraria/Images/Item_321";

        public override void SetDefaults()
        {
            Item.width        = 32;
            Item.height       = 48;
            Item.maxStack     = 99;
            Item.useTurn      = true;    // 可在挥动时改变朝向
            Item.autoReuse    = false;
            Item.useTime      = 15;
            Item.useAnimation = 15;
            Item.useStyle     = ItemUseStyleID.Swing;
            Item.consumable   = true;
            Item.createTile   = ModContent.TileType<HuangQuanAltarTile>();
            Item.placeStyle   = 0;
            Item.value        = Item.buyPrice(gold: 50);
            Item.rare         = ItemRarityID.Purple; // 紫色稀有度
        }
    }
}
