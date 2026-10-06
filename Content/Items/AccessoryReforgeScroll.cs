using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Content.Items
{
    public class AccessoryReforgeScroll : ModItem
    {
        // 贴图使用默认路径 Content/Items/AccessoryReforgeScroll.png

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 20;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.buyPrice(platinum: 20);
        }

        // 打开界面不消耗；服务在实际修改饰品后扣除一张券。
        public override bool ConsumeItem(Player player) => false;

        public override bool? UseItem(Player player)
        {
            if (!Main.dedServ && player.whoAmI == Main.myPlayer)
                ModContent.GetInstance<BatchReforgeUISystem>().Open(BatchReforgeKind.Accessory);
            return true;
        }
    }
}
