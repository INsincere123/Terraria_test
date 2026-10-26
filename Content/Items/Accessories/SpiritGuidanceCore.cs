using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Players;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Accessories
{
    public sealed class SpiritGuidanceCore : ModItem
    {
        // 每物品实例保存，值类型沿标准Clone复制；新物品及旧存档默认保留模组支持。
        public bool ModdedShotsEnabled { get; private set; } = true;

        internal void ToggleModdedShots()
        {
            ModdedShotsEnabled = !ModdedShotsEnabled;
            Item.NetStateChanged();
        }

        public override void SaveData(TagCompound tag) => tag["moddedShotsEnabled"] = ModdedShotsEnabled;
        public override void LoadData(TagCompound tag) =>
            ModdedShotsEnabled = !tag.ContainsKey("moddedShotsEnabled") || tag.GetBool("moddedShotsEnabled");
        public override void NetSend(BinaryWriter writer) => writer.Write(ModdedShotsEnabled);
        public override void NetReceive(BinaryReader reader) => ModdedShotsEnabled = reader.ReadBoolean();

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "ModdedShotsState", Language.GetTextValue(
                "Mods.TestMod.Items.SpiritGuidanceCore." + (ModdedShotsEnabled ? "ModdedShotsOn" : "ModdedShotsOff")))
            { OverrideColor = ModdedShotsEnabled ? Color.LightGreen : Color.Gray });
            tooltips.Add(new TooltipLine(Mod, "ModdedShotsHint",
                Language.GetTextValue("Mods.TestMod.Items.SpiritGuidanceCore.ToggleHint")));
        }

        public override void SetDefaults()
        {
            Item.width = Item.height = 30;
            Item.accessory = true;
            Item.rare = ModContent.RarityType<TerraRarity>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            var state = player.GetModPlayer<SpiritGuidancePlayer>();
            state.Enabled = true;
            state.ModdedShotsEnabled |= ModdedShotsEnabled;
            SummonCritPlayer.Enable(player);
            player.GetModPlayer<SummonCritPlayer>().BonusCritChance += 1f;
        }

    }
}
