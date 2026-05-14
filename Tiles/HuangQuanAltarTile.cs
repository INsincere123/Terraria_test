using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using TestMod.Items;

namespace TestMod.Tiles
{
    /// <summary>
    /// 黄泉馈灵塔 Tile — 2×3 多格家具，放置后激活范围效果。
    ///
    /// ⚠️ 贴图占位说明：
    ///   当前 Texture 指向原版 Lunar Crafting Station 贴图（仅供开发期测试）。
    ///   正式使用时，请在 Tiles/HuangQuanAltarTile.png 提供自定义贴图
    ///   （尺寸：36×54 像素；布局：2 列 × 3 行，每格 16px，列/行间距 2px）
    ///   并删除下面的 Texture override。
    /// </summary>
    public class HuangQuanAltarTile : ModTile
    {
        // 开发期占位贴图；替换为自定义 PNG 后删除此 override
        public override string Texture => "Terraria/Images/Tiles_399";

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;  // 多格 tile 必须：保存帧偏移数据
            Main.tileLavaDeath[Type]      = false; // 不被岩浆摧毁
            Main.tileNoFail[Type]         = true;  // 挖掘尝试不会随机失败

            // 以 Style2x2 为基础扩展到 2×3
            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Height           = 3;
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
            TileObjectData.newTile.Origin            = new Point16(0, 2); // 锚点：底部左格
            TileObjectData.newTile.AnchorBottom      = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop, 2, 0);
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(100, 30, 140), CreateMapEntryName()); // 深紫色地图标记
            DustType = DustID.Shadowflame; // 破坏时产生暗焰粒子
        }

        /// <summary>
        /// 多格 tile 整体被破坏时调用（i/j 为左上角格坐标）。
        /// tModLoader 保证每次只调用一次，直接掉落物品即可。
        /// </summary>
        public override void KillMultiTile(int i, int j, int frameX, int frameY)
        {
            Item.NewItem(new EntitySource_TileBreak(i, j),
                i * 16, j * 16, 32, 48,
                ModContent.ItemType<HuangQuanAltarItem>());
        }
    }
}
