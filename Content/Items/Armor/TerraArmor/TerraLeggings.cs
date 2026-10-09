using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Armor.TerraArmor
{
    public class TerraLeggings : TerraArmorItem
    {
        protected override EquipType ArmorPart => EquipType.Legs;
        protected override int VanillaItem(TerraArmorMode mode) => mode switch
        {
            TerraArmorMode.Ranger => ItemID.VortexLeggings,
            TerraArmorMode.Mage => ItemID.NebulaLeggings,
            TerraArmorMode.Summoner => ItemID.StardustLeggings,
            _ => ItemID.SolarFlareLeggings
        };
        protected override int VanillaSlot(TerraArmorMode mode) => mode switch
        {
            TerraArmorMode.Ranger => ArmorIDs.Legs.VortexLeggings,
            TerraArmorMode.Mage => ArmorIDs.Legs.NebulaLeggings,
            TerraArmorMode.Summoner => ArmorIDs.Legs.StardustLeggings,
            _ => ArmorIDs.Legs.SolarFlareLeggings
        };
        public override void AddRecipes() => RegisterRecipe(ItemID.SolarFlareLeggings, ItemID.VortexLeggings,
            ItemID.NebulaLeggings, ItemID.StardustLeggings);
    }
}
