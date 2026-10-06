using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items
{
    public class MapRevealScroll : ModItem
    {
        // 占位贴图：魔法灯笼，替换为自定义 PNG 后删除 Texture override
        public override string Texture => "Terraria/Images/Item_" + ItemID.MagicLantern;

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 26;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item4;
            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.autoReuse = false;
            Item.consumable = true;
            Item.maxStack = 9999;
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.value = Item.buyPrice(gold: 1);
        }

        public override bool? UseItem(Player player)
        {
            // 参考 HEROsMod.HEROsModServices.MapRevealer.RevealWholeMap：
            // 遍历全图调用 Main.Map.Update(i, j, 255)（255 = 全亮），再置 refreshMap 触发重绘。
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // 远程客户端：地图探索数据在本地，只点亮当前已加载的区块
                for (int i = 0; i < Main.maxTilesX; i++)
                {
                    for (int j = 0; j < Main.maxTilesY; j++)
                    {
                        if (WorldGen.InWorld(i, j) && Main.sectionManager.TileLoaded(i, j))
                        {
                            Main.Map.Update(i, j, 255);
                        }
                    }
                }
            }
            else if (!Main.dedServ)
            {
                // 单机 / 联机主机：全图点亮（主机客户端与服务器同进程，Main.Map 即主机显示的地图）
                // 专用服务器无地图可渲染，跳过循环避免无谓开销
                for (int i = 0; i < Main.maxTilesX; i++)
                {
                    for (int j = 0; j < Main.maxTilesY; j++)
                    {
                        if (WorldGen.InWorld(i, j))
                        {
                            Main.Map.Update(i, j, 255);
                        }
                    }
                }
            }
            Main.refreshMap = true;
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.BottledWater)
                .AddIngredient(ItemID.FallenStar, 5)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
}
