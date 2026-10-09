using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Accessories
{
    public enum VortexAmmoMode : byte { Default, Full, Random, Simple }

    public abstract class VortexAmmoAccessory : ModItem
    {
        public VortexAmmoMode Mode { get; private set; }
        public abstract int AmmoCategory { get; }
        protected abstract int Ingredient { get; }
        protected virtual void ApplyAmmoStats(Player player) { }
        private string TextKey => $"Mods.TestMod.Items.{Name}.";
        public override string Texture => $"Terraria/Images/Item_{Ingredient}";
        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(gold: 10);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 同类饰品以装备栏首件为准；外部允许重复装备时也不重复注入属性。
            if (!player.GetModPlayer<VortexQuiverPlayer>().Equip(this)) return;
            ApplyAmmoStats(player);
            player.GetCritChance(DamageClass.Ranged) += 5f;
        }

        public override bool CanRightClick() => Main.keyState.PressingShift();
        public override bool ConsumeItem(Player player) => false;
        public override void RightClick(Player player)
        {
            Mode = (VortexAmmoMode)(((byte)Mode + 1) % 4);
            Item.NetStateChanged();
        }
        public override void SaveData(TagCompound tag) => tag["ammoMode"] = (int)Mode;
        public override void LoadData(TagCompound tag) => Mode = ValidMode(tag.GetInt("ammoMode"));
        public override void NetSend(BinaryWriter writer) => writer.Write((byte)Mode);
        public override void NetReceive(BinaryReader reader) => Mode = ValidMode(reader.ReadByte());
        private static VortexAmmoMode ValidMode(int mode)
            => mode >= 0 && mode <= 3 ? (VortexAmmoMode)mode : VortexAmmoMode.Default;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "AmmoMode", Language.GetTextValue(TextKey + "Mode" + Mode)));
            tooltips.Add(new TooltipLine(Mod, "ModeToggle", Language.GetTextValue("Mods.TestMod.VortexAmmo.ToggleHint")));
            if (!VortexQuiverSystem.AmmoReady)
                tooltips.Add(new TooltipLine(Mod, "AmmoUnavailable", Language.GetTextValue("Mods.TestMod.VortexAmmo.AmmoUnavailable")));
            if (AmmoCategory == AmmoID.Arrow && !VortexQuiverSystem.GravityReady)
                tooltips.Add(new TooltipLine(Mod, "GravityUnavailable", Language.GetTextValue(TextKey + "GravityUnavailable")));
        }

        public override void AddRecipes() => CreateRecipe().AddIngredient(Ingredient)
            .AddIngredient(ItemID.FragmentVortex, 12).AddTile(TileID.LunarCraftingStation).Register();
    }
}
