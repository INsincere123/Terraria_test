using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace 武器test.Items
{
    public class BuffToggleItem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.value = Item.buyPrice(0, 10, 0, 0);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item4;
            Item.autoReuse = false;
        }

        public override bool? UseItem(Player player)
        {
            player.GetModPlayer<MyPlayer>().godModeBuff =
                !player.GetModPlayer<MyPlayer>().godModeBuff;
            return true;
        }

        public override void AddRecipes()
        {
            Recipe recipe = Recipe.Create(Type);

            // 添加所有大师模式圣物（Master Trophies）
            recipe.AddIngredient(4924); // 克苏鲁之眼
            recipe.AddIngredient(4925); // 世界吞噬怪
            recipe.AddIngredient(4926); // 克苏鲁之脑
            recipe.AddIngredient(4927); // 骷髅王
            recipe.AddIngredient(4928); // 蜂王
            recipe.AddIngredient(4929); // 史莱姆王
            recipe.AddIngredient(4930); // 血肉墙
            recipe.AddIngredient(4931); // 双子魔眼
            recipe.AddIngredient(4932); // 毁灭者
            recipe.AddIngredient(4933); // 机械骷髅王
            recipe.AddIngredient(4934); // 世纪之花
            recipe.AddIngredient(4935); // 石巨人
            recipe.AddIngredient(4936); // 猪龙鱼公爵
            recipe.AddIngredient(4937); // 拜月教邪教徒
            recipe.AddIngredient(4938); // 月亮领主
            recipe.AddIngredient(4939); // 火星飞碟
            recipe.AddIngredient(4940); // 荷兰飞盗船
            recipe.AddIngredient(4941); // 哀木
            recipe.AddIngredient(4942); // 南瓜王
            recipe.AddIngredient(4943); // 冰雪女王
            recipe.AddIngredient(4944); // 常绿尖叫怪
            recipe.AddIngredient(4945); // 圣诞坦克
            recipe.AddIngredient(4946); // 暗黑魔法师
            recipe.AddIngredient(4947); // 食人魔
            recipe.AddIngredient(4948); // 双足翼龙
            recipe.AddIngredient(4949); // 光之女皇
            recipe.AddIngredient(4950); // 史莱姆皇后
            recipe.AddIngredient(5110); // 独眼巨鹿

            recipe.AddTile(TileID.LunarCraftingStation); // 月亮合成站
            recipe.Register();
        }
    }
}