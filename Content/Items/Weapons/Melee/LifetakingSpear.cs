using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Common.Systems;

namespace TestMod.Content.Items.Weapons.Melee
{
    public sealed class LifetakingSpear : ModItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.DayBreak}";

        public override void SetDefaults()
        {
            Item.CloneDefaults(ItemID.DayBreak);
            Item.shoot = ModContent.ProjectileType<Projectiles.Melee.LifetakingSpearProjectile>();
            Item.autoReuse = false;
        }

        public override bool CanUseItem(Player player)
            => LifetakingAttackSystem.Ready && player.GetModPlayer<LifetakingPlayer>().Cooldown == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
            Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.whoAmI == Main.myPlayer)
                player.GetModPlayer<LifetakingPlayer>().RequestThrow(velocity);
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            if (Main.LocalPlayer.TryGetModPlayer(out LifetakingPlayer state) && state.Cooldown > 0)
                tooltips.Add(new TooltipLine(Mod, "LifetakingCooldown",
                    Language.GetTextValue("Mods.TestMod.Lifetaking.Cooldown", (state.Cooldown + 59) / 60)));
        }

        public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.DayBreak)
            .AddTile(TileID.LunarCraftingStation).Register();
    }
}
