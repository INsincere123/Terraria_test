using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Utilities;

namespace TestMod.Content.Items.Armor
{
    /// <summary>物品实例上的套装效果开关；不影响盔甲的普通属性。</summary>
    public abstract class ToggleableArmorItem : ModItem
    {
        private const string SaveKey = "effectEnabled";
        private const string LocalizationPrefix = "Mods.TestMod.ArmorToggle.";

        // 值类型状态随 ModItem.Clone 复制，新物品和缺少此字段的旧存档默认开启。
        public bool EffectEnabled { get; private set; } = true;

        protected abstract string ToggleEffectNameKey { get; }

        public static string GetEffectStateText(bool enabled)
            => Language.GetTextValue(LocalizationPrefix + (enabled ? "Enabled" : "Disabled"));

        public override bool CanRightClick() => Main.keyState.PressingShift();

        public override void RightClick(Player player)
        {
            EffectEnabled = !EffectEnabled;
            Item.NetStateChanged();
        }

        public override bool ConsumeItem(Player player) => false;

        public override void SaveData(TagCompound tag) => tag[SaveKey] = EffectEnabled;

        public override void LoadData(TagCompound tag)
            => EffectEnabled = !tag.ContainsKey(SaveKey) || tag.GetBool(SaveKey);

        public override void NetSend(BinaryWriter writer) => writer.Write(EffectEnabled);

        public override void NetReceive(BinaryReader reader) => EffectEnabled = reader.ReadBoolean();

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "ArmorEffectState", Language.GetTextValue(
                LocalizationPrefix + "State", Language.GetTextValue(ToggleEffectNameKey), GetEffectStateText(EffectEnabled)))
            {
                OverrideColor = EffectEnabled ? Color.LightGreen : Color.Gray,
            });
            tooltips.Add(new TooltipLine(Mod, "ArmorToggleHint", Language.GetTextValue(LocalizationPrefix + "Hint")));
            tooltips.Add(new TooltipLine(Mod, "ArmorToggleRequirement", Language.GetTextValue(LocalizationPrefix + "RequiresFullSet")));
        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale)
            => DrawUtils.DrawInventoryToggle(spriteBatch, position, EffectEnabled);
    }
}
