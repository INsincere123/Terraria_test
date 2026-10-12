using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using TestMod.Common.Mechanics.Dashes;
using TestMod.Common.Players;
using TestMod.Content.Items.Accessories;

namespace TestMod.Common.Systems
{
    public sealed class CompositeAccessorySystem : ModSystem
    {
        private Hook movementHook, rightClickHook;
        private int previousBrainTransform, previousScarfTransform;
        private bool shimmerInstalled;

        public override void SetStaticDefaults()
        {
            previousBrainTransform = ItemID.Sets.ShimmerTransformToItem[ItemID.BrainOfConfusion];
            previousScarfTransform = ItemID.Sets.ShimmerTransformToItem[ItemID.WormScarf];
            ItemID.Sets.ShimmerTransformToItem[ItemID.BrainOfConfusion] = ItemID.WormScarf;
            ItemID.Sets.ShimmerTransformToItem[ItemID.WormScarf] = ItemID.BrainOfConfusion;
            shimmerInstalled = true;
        }

        public override void AddRecipes()
        {
            Recipe.Create(ItemID.CobaltShield).AddIngredient(ItemID.CobaltBar, 10).AddTile(TileID.Anvils).Register();
            Recipe.Create(ItemID.CobaltShield).AddIngredient(ItemID.PalladiumBar, 10).AddTile(TileID.Anvils).Register();
        }

        public override void Load()
        {
            try
            {
                movementHook = new Hook(typeof(Player).GetMethod(nameof(Player.DashMovement)),
                    (Action<Action<Player>, Player>)Move);
                MethodInfo click = typeof(ItemSlot).GetMethod(nameof(ItemSlot.RightClick),
                    new[] { typeof(Item[]), typeof(int), typeof(int) });
                rightClickHook = new Hook(click, (Action<Action<Item[], int, int>, Item[], int, int>)RightClick);
            }
            catch
            {
                Unload();
                throw;
            }
        }

        private static void Move(Action<Player> original, Player player)
        {
            if (player.TryGetModPlayer(out DashPlayer dash) && dash.ActiveEffect is CompositePhaseDash effect)
                effect.Move(player);
            else original(player);
        }

        private static void RightClick(Action<Item[], int, int> original, Item[] inventory, int context, int slot)
        {
            if (!Main.dedServ && Main.keyState.PressingShift()
                && context is ItemSlot.Context.InventoryItem or ItemSlot.Context.EquipAccessory
                    or ItemSlot.Context.EquipAccessoryVanity or ItemSlot.Context.ModdedAccessorySlot
                    or ItemSlot.Context.ModdedVanityAccessorySlot
                && slot >= 0 && slot < inventory.Length && inventory[slot]?.ModItem is PanicToggleAccessory accessory)
            {
                if (Main.mouseRight && Main.mouseRightRelease)
                {
                    accessory.TogglePanic();
                    Main.mouseRightRelease = false;
                }
                return;
            }
            original(inventory, context, slot);
        }

        public override void OnWorldUnload()
        {
            foreach (Player player in Main.player)
                if (player?.active == true && player.TryGetModPlayer(out CompositeAccessoryPlayer state)) state.ResetSession();
        }

        public override void Unload()
        {
            rightClickHook?.Dispose();
            movementHook?.Dispose();
            rightClickHook = movementHook = null;
            if (shimmerInstalled)
            {
                // 只撤销自己的映射，不覆盖加载后由其他模组接管的值。
                if (ItemID.Sets.ShimmerTransformToItem[ItemID.BrainOfConfusion] == ItemID.WormScarf)
                    ItemID.Sets.ShimmerTransformToItem[ItemID.BrainOfConfusion] = previousBrainTransform;
                if (ItemID.Sets.ShimmerTransformToItem[ItemID.WormScarf] == ItemID.BrainOfConfusion)
                    ItemID.Sets.ShimmerTransformToItem[ItemID.WormScarf] = previousScarfTransform;
                shimmerInstalled = false;
            }
        }
    }
}
