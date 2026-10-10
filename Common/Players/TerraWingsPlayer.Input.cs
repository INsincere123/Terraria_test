using Terraria;
using Terraria.ID;
using Terraria.UI;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.Players
{
    public partial class TerraWingsPlayer
    {
        private bool CanToggleSlowMode(Item[] inventory, int context, int slot)
            => Player.whoAmI == Main.myPlayer && Main.keyState.PressingShift()
                && context is ItemSlot.Context.InventoryItem or ItemSlot.Context.EquipAccessory
                    or ItemSlot.Context.EquipAccessoryVanity or ItemSlot.Context.ModdedAccessorySlot
                    or ItemSlot.Context.ModdedVanityAccessorySlot
                && slot >= 0 && slot < inventory.Length && inventory[slot]?.ModItem is TerraWings;

        public override bool HoverSlot(Item[] inventory, int context, int slot)
        {
            if (!CanToggleSlowMode(inventory, context, slot)) return false;
            Main.cursorOverride = CursorOverrideID.DefaultCursor;
            return true;
        }

        public override bool ShiftClickSlot(Item[] inventory, int context, int slot)
        {
            if (!CanToggleSlowMode(inventory, context, slot)) return false;
            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                ((TerraWings)inventory[slot].ModItem).ToggleSlowMode();
                Main.mouseLeftRelease = false;
            }
            // 只消费本饰品的点击，阻止本次快捷出售/丢弃/移动，不改写游戏中的攻击输入。
            return true;
        }
    }
}
