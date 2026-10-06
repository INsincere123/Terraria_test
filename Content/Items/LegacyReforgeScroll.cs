using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TestMod.Content.Items
{
    // 只保留旧存档的物品身份；批量重铸由独立模组提供。
    public abstract class LegacyReforgeScroll : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = Item.height = 32;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.buyPrice(platinum: 20);
        }

        public override bool CanUseItem(Player player) => false;
        public override bool ConsumeItem(Player player) => false;

        public override void LoadData(TagCompound tag)
        {
            if (!ModContent.TryFind<ModItem>("MassReforge", Name, out var replacement))
                return;

            // ItemIO 在此后恢复 prefix、stack、favorited 与 GlobalItem 数据。
            // 直接替换类型，下一次保存便采用新模组身份，不需要扫描各存储槽。
            Item.SetDefaults(replacement.Type);
        }
    }
}
