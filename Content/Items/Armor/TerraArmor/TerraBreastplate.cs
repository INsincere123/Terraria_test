using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Armor.TerraArmor
{
    public class TerraBreastplate : TerraArmorItem
    {
        protected override EquipType ArmorPart => EquipType.Body;
        protected override int VanillaItem(TerraArmorMode mode) => mode switch
        {
            TerraArmorMode.Ranger => ItemID.VortexBreastplate,
            TerraArmorMode.Mage => ItemID.NebulaBreastplate,
            TerraArmorMode.Summoner => ItemID.StardustBreastplate,
            _ => ItemID.SolarFlareBreastplate
        };
        protected override int VanillaSlot(TerraArmorMode mode) => mode switch
        {
            TerraArmorMode.Ranger => ArmorIDs.Body.VortexBreastplate,
            TerraArmorMode.Mage => ArmorIDs.Body.NebulaBreastplate,
            TerraArmorMode.Summoner => ArmorIDs.Body.StardustPlate,
            _ => ArmorIDs.Body.SolarFlareBreastplate
        };

        public override void AddRecipes() => RegisterRecipe(ItemID.SolarFlareBreastplate, ItemID.VortexBreastplate,
            ItemID.NebulaBreastplate, ItemID.StardustBreastplate);
    }
}
