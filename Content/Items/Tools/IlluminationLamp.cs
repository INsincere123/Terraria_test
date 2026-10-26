using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Items.Tools
{
    public sealed class IlluminationLamp : ModItem
    {
        public override string Texture => "Terraria/Images/Item_4702";

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 30;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Yellow;
        }
    }
}
