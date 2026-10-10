using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Players;
using TestMod.Common.Configs;
using TestMod.Common.Mechanics.Dashes;
using TestMod.Common.Systems;
using TestMod.Common.Utilities;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Accessories
{
    public class TerraWings : ModItem
    {
        public const int FlightTime = 900;
        public const float FlightSpeed = 18f;
        public const float FlightAcceleration = 0.75f;
        // 原版速度表换算：像素/帧 × 216000 / 42240 = mph。
        internal const float SlowModeSpeed = 55f * 42240f / 216000f;
        private const string TextKey = "Mods.TestMod.Items.TerraWings.";
        private const string AssetPath = "TestMod/Assets/Textures/Accessories/TerraWings";
        internal static readonly Color EmeraldColor = new(50, 220, 130);

        // 注册资源由所有物品实例共享；NewInstance 默认重新构造，不复制加载模板的字段。
        private static readonly int[] visualSlots = new int[5];
        private static int emeraldShader;
        // 值类型随 ModItem.Clone 复制；新物品及缺字段的存档默认关闭。
        public bool ExtraJumpsEnabled { get; private set; }
        public bool SlowModeEnabled { get; private set; }
        public override string Texture => AssetPath;
        internal int LogicSlot => Item.wingSlot;
        internal int EmeraldShader => emeraldShader;

        public override void Load()
        {
            // 不关联 Load 阶段尚未注册的物品 Type；由 AutoDefaults 绑定默认槽。
            visualSlots[0] = EquipLoader.AddEquipTexture(Mod, AssetPath + "_Wings", EquipType.Wings,
                name: Name);
            foreach (TerraArmorMode mode in Enum.GetValues<TerraArmorMode>())
                visualSlots[(int)mode + 1] = EquipLoader.AddEquipTexture(Mod,
                    "Terraria/Images/Wings_" + VanillaWing(mode), EquipType.Wings,
                    name: Name + "_" + mode);
        }

        public override void AutoDefaults()
        {
            base.AutoDefaults();
            Item.wingSlot = EquipLoader.GetEquipSlot(Mod, Name, EquipType.Wings);
        }

        public override void Unload()
        {
            Array.Clear(visualSlots);
            emeraldShader = 0;
        }

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            foreach (int slot in visualSlots)
                ArmorIDs.Wing.Sets.Stats[slot] = new WingStats(FlightTime, FlightSpeed,
                    hasHoldDownHoverFeatures: true, hoverFlySpeedOverride: FlightSpeed);
            if (Main.dedServ) return;
            // 复用原版逐像素重着色通道，不修改源图片；卸载由 tML 的染料加载器统一清理。
            GameShaders.Armor.BindShader(Type, new ArmorShaderData(
                Main.Assets.Request<Effect>("PixelShader", AssetRequestMode.ImmediateLoad), "ArmorColored"))
                .UseColor(EmeraldColor);
            emeraldShader = GameShaders.Armor.GetShaderIdFromItemId(Type);
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 32;
            Item.accessory = true;
            // 原版在 ModItem.SetDefaults 前按染色器绑定设置 dye 和可堆叠上限。
            // 两者都须还原，否则 maxStack != 1 会阻止普通右键快捷装备。
            Item.dye = 0;
            Item.maxStack = 1;
            Item.value = Item.sellPrice(gold: 40);
            Item.rare = ModContent.RarityType<TerraRarity>();
        }

        internal bool OwnsSlot(int slot) => Array.IndexOf(visualSlots, slot) >= 0;
        internal int CurrentVisualSlot(Player player) => HasTerraSet(player, out TerraArmorPlayer armor)
            ? visualSlots[(int)armor.Mode + 1] : LogicSlot;

        private static bool HasTerraSet(Player player, out TerraArmorPlayer armor)
        {
            // 同时核对当前功能装备，防止绘制阶段读取上一帧 FullSet。
            return player.TryGetModPlayer(out armor) && TerraArmorPlayer.Equipped(player);
        }

        private static int VanillaWing(TerraArmorMode mode) => mode switch
        {
            TerraArmorMode.Ranger => ArmorIDs.Wing.VortexBooster,
            TerraArmorMode.Mage => ArmorIDs.Wing.NebulaMantle,
            TerraArmorMode.Summoner => ArmorIDs.Wing.StardustWings,
            _ => ArmorIDs.Wing.SolarWings,
        };

        public override void UpdateAccessory(Player player, bool hideVisual)
            => player.GetModPlayer<TerraWingsPlayer>().Equip(this, hideVisual);

        public override void HorizontalWingSpeeds(Player player, ref float speed, ref float acceleration)
        {
            if (!FargoAccessoryCompatSystem.TryHorizontalWingSpeeds(player, ref speed, ref acceleration))
            {
                speed = FlightSpeed;
                acceleration = FlightAcceleration;
            }
            if (SlowModeEnabled && player.GetModPlayer<DashPlayer>().ActiveEffect == null)
                speed = Math.Min(speed, SlowModeSpeed);
        }

        public override void VerticalWingSpeeds(Player player, ref float ascentWhenFalling,
            ref float ascentWhenRising, ref float maxCanAscendMultiplier, ref float maxAscentMultiplier,
            ref float constantAscend)
        {
            ascentWhenFalling = 5.1f;
            ascentWhenRising = 1f;
            maxCanAscendMultiplier = 1f;
            maxAscentMultiplier = 1.75f;
            constantAscend = 0.54f;
        }

        public override bool CanRightClick() => Main.keyState.PressingShift();
        public override bool ConsumeItem(Player player) => false;
        public override void RightClick(Player player)
        {
            ExtraJumpsEnabled = !ExtraJumpsEnabled;
            Item.NetStateChanged();
        }
        internal void ToggleSlowMode()
        {
            SlowModeEnabled = !SlowModeEnabled;
            Item.NetStateChanged();
        }
        public override void SaveData(TagCompound tag)
        {
            tag["extraJumps"] = ExtraJumpsEnabled;
            tag["slowMode"] = SlowModeEnabled;
        }
        public override void LoadData(TagCompound tag)
        {
            ExtraJumpsEnabled = tag.GetBool("extraJumps");
            SlowModeEnabled = tag.GetBool("slowMode");
        }
        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(ExtraJumpsEnabled);
            writer.Write(SlowModeEnabled);
        }
        public override void NetReceive(BinaryReader reader)
        {
            ExtraJumpsEnabled = reader.ReadBoolean();
            SlowModeEnabled = reader.ReadBoolean();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string dashHint = TestModClientConfig.Instance?.SingleTapDash == true
                ? Language.GetTextValue(TextKey + "SingleKeyDash", KeybindUtils.GetKeyText(DashKeybinds.VanillaDashKey))
                : Language.GetTextValue(TextKey + "DoubleTapDash");
            tooltips.Add(new TooltipLine(Mod, "DashHint", dashHint));
            tooltips.Add(new TooltipLine(Mod, "ExtraJumpState",
                Language.GetTextValue(TextKey + (ExtraJumpsEnabled ? "JumpsEnabled" : "JumpsDisabled")))
            { OverrideColor = ExtraJumpsEnabled ? Color.LightGreen : Color.Gray });
            tooltips.Add(new TooltipLine(Mod, "ToggleHint", Language.GetTextValue("Mods.TestMod.ArmorToggle.Hint")));
            tooltips.Add(new TooltipLine(Mod, "SlowModeState",
                Language.GetTextValue(TextKey + (SlowModeEnabled ? "SlowModeEnabled" : "SlowModeDisabled")))
            { OverrideColor = SlowModeEnabled ? Color.LightGreen : Color.Gray });
            tooltips.Add(new TooltipLine(Mod, "SlowModeHint", Language.GetTextValue(TextKey + "SlowModeHint")));
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale)
            => DrawUtils.DrawInventoryToggle(spriteBatch, position, ExtraJumpsEnabled);

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.WingsNebula).AddIngredient(ItemID.WingsVortex)
            .AddIngredient(ItemID.WingsStardust).AddIngredient(ItemID.WingsSolar)
            .AddIngredient(ItemID.TerrasparkBoots).AddIngredient(ItemID.HorseshoeBundle)
            .AddIngredient(ItemID.Magiluminescence).AddIngredient(ItemID.EmpressFlightBooster)
            .AddTile(TileID.LunarCraftingStation).Register();
    }
}
