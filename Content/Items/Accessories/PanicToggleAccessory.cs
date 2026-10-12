using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using TestMod.Common.Configs;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Accessories
{
    // 开关属于物品实例；值类型由标准 Clone 复制，不保存到玩家或加载模板。
    public abstract class PanicToggleAccessory : ModItem
    {
        public bool PanicEnabled { get; private set; } = true;
        protected virtual bool HasDash => false;

        internal void TogglePanic()
        {
            PanicEnabled = !PanicEnabled;
            Item.NetStateChanged();
        }

        public override void SaveData(TagCompound tag) => tag["panicEnabled"] = PanicEnabled;
        public override void LoadData(TagCompound tag) =>
            PanicEnabled = !tag.ContainsKey("panicEnabled") || tag.GetBool("panicEnabled");
        public override void NetSend(BinaryWriter writer) => writer.Write(PanicEnabled);
        public override void NetReceive(BinaryReader reader) => PanicEnabled = reader.ReadBoolean();

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            if (HasDash) AddDashHint(tooltips, Mod);
            tooltips.Add(new TooltipLine(Mod, "PanicState", Language.GetTextValue(
                "Mods.TestMod.CompositeAccessories." + (PanicEnabled ? "PanicEnabled" : "PanicDisabled")))
            { OverrideColor = PanicEnabled ? Color.LightGreen : Color.Gray });
            tooltips.Add(new TooltipLine(Mod, "PanicHint",
                Language.GetTextValue("Mods.TestMod.CompositeAccessories.PanicHint")));
        }

        internal static void AddDashHint(List<TooltipLine> tooltips, Mod mod)
        {
            string hint = TestModClientConfig.Instance?.SingleTapDash == true
                ? Language.GetTextValue("Mods.TestMod.CompositeAccessories.SingleKeyDash", KeybindUtils.GetKeyText(DashKeybinds.VanillaDashKey))
                : Language.GetTextValue("Mods.TestMod.CompositeAccessories.DoubleTapDash");
            tooltips.Add(new TooltipLine(mod, "DashHint", hint));
        }
    }
}
