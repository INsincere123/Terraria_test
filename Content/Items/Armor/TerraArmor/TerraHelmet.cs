using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Armor.TerraArmor
{
    public class TerraHelmet : TerraArmorItem
    {
        // 每件头盔独立保存；新物品和旧存档缺字段时默认开启。
        public bool LightEnabled { get; private set; } = true;

        public override bool CanRightClick() => Main.keyState.PressingShift();

        public override void RightClick(Player player)
        {
            LightEnabled = !LightEnabled;
            Item.NetStateChanged();
        }

        public override bool ConsumeItem(Player player) => false;

        public override void SaveData(TagCompound tag) => tag["lightEnabled"] = LightEnabled;

        public override void LoadData(TagCompound tag)
            => LightEnabled = !tag.ContainsKey("lightEnabled") || tag.GetBool("lightEnabled");

        public override void NetSend(BinaryWriter writer) => writer.Write(LightEnabled);

        public override void NetReceive(BinaryReader reader) => LightEnabled = reader.ReadBoolean();

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "TerraLightState", Language.GetTextValue(
                "Mods.TestMod.TerraArmor.LightState", GetLightStateText()))
            {
                OverrideColor = LightEnabled ? Color.LightGreen : Color.Gray
            });
            tooltips.Add(new TooltipLine(Mod, "TerraLightHint",
                Language.GetTextValue("Mods.TestMod.ArmorToggle.Hint")));
        }

        private string GetLightStateText() => Language.GetTextValue(
            "Mods.TestMod.ArmorToggle." + (LightEnabled ? "Enabled" : "Disabled"));

        internal static bool ShouldIlluminateViewport()
        {
            if (Main.dedServ || Main.gameMenu) return false;
            Player player = Main.LocalPlayer;
            return player.active && !player.dead &&
                player.armor[0].ModItem is TerraHelmet helmet && helmet.LightEnabled;
        }

        protected override EquipType ArmorPart => EquipType.Head;
        protected override int VanillaItem(TerraArmorMode mode) => mode switch
        {
            TerraArmorMode.Ranger => ItemID.VortexHelmet,
            TerraArmorMode.Mage => ItemID.NebulaHelmet,
            TerraArmorMode.Summoner => ItemID.StardustHelmet,
            _ => ItemID.SolarFlareHelmet
        };
        protected override int VanillaSlot(TerraArmorMode mode) => mode switch
        {
            TerraArmorMode.Ranger => ArmorIDs.Head.VortexHelmet,
            TerraArmorMode.Mage => ArmorIDs.Head.NebulaHelmet,
            TerraArmorMode.Summoner => ArmorIDs.Head.StardustHelmet,
            _ => ArmorIDs.Head.SolarFlareHelmet
        };

        public override bool IsArmorSet(Item head, Item body, Item legs) =>
            body.type == ModContent.ItemType<TerraBreastplate>() && legs.type == ModContent.ItemType<TerraLeggings>();

        public override void UpdateArmorSet(Player player) => player.GetModPlayer<TerraArmorPlayer>().ApplySet();

        public override void AddRecipes() => RegisterRecipe(ItemID.SolarFlareHelmet, ItemID.VortexHelmet,
            ItemID.NebulaHelmet, ItemID.StardustHelmet);
    }
}
