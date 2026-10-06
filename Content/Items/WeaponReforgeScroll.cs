using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Content.Items
{
    public class WeaponReforgeScroll : ModItem
    {
        // 贴图使用默认路径 Content/Items/WeaponReforgeScroll.png

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

        // 打开不扣券，确认实际修改武器后由批量服务统一消耗。
        public override bool ConsumeItem(Player player) => false;

        public override bool? UseItem(Player player)
        {
            if (!Main.dedServ && player.whoAmI == Main.myPlayer)
                ModContent.GetInstance<BatchReforgeUISystem>().Open(BatchReforgeKind.Weapon);
            return true;
        }
    }
}
