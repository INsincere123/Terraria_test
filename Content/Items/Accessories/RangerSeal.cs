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
    /// <summary>
    /// 射手之证：由弹药箱、熔火箭袋、狙击镜合成。
    /// 一件饰品同时为箭/子弹/火箭三类提供星璇式弹种轮换，Shift+右键切换模式。
    /// </summary>
    public class RangerSeal : ModItem
    {
        public VortexAmmoMode Mode { get; private set; } = VortexAmmoMode.Default;
        private string TextKey => $"Mods.TestMod.Items.{Name}.";

        public override string Texture => $"Terraria/Images/Item_{ItemID.RangerEmblem}";

        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(gold: 10);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            if (!player.GetModPlayer<VortexQuiverPlayer>().EquipRangerSeal(this)) return;
            // 自定义数值
            player.aggro -= 400;
            // 省弹由 GlobalItem.CanConsumeAmmo 独立掷骰 20%，不复用 ammoBox，可与弹药箱 Buff 叠加。
            player.GetDamage(DamageClass.Ranged) += 0.25f;
            player.GetCritChance(DamageClass.Ranged) += 10f;

            // 复用原版箭袋功能，含箭/木桩初速、击退及省箭；不注入箭伤害属性。
            // 与星璇箭袋共存时布尔效果只生效一次，普通箭转换也沿原版顺序执行。
            player.magicQuiver = true;
            player.hasMoltenQuiver = true;
        }

        public override bool CanRightClick() => Main.keyState.PressingShift();
        public override bool ConsumeItem(Player player) => false;
        public override void RightClick(Player player)
        {
            Mode = (VortexAmmoMode)(((byte)Mode + 1) % 4);
            Item.NetStateChanged();
            // 仅更新由这件物品控制的类别；同类别星璇的模式不受影响。
            player.GetModPlayer<VortexQuiverPlayer>().SetRangerSealMode(this);
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
            if (!VortexQuiverSystem.GravityReady)
                tooltips.Add(new TooltipLine(Mod, "GravityUnavailable", Language.GetTextValue("Mods.TestMod.Items.VortexQuiver.GravityUnavailable")));
        }

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.AmmoBox)
            .AddIngredient(ItemID.MoltenQuiver)
            .AddIngredient(ItemID.SniperScope)
            .AddTile(TileID.TinkerersWorkbench)
            .Register();
    }
}
