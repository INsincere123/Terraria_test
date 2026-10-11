using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Content.Buffs;
using TestMod.Content.Projectiles.Minions;
using TestMod.Content.Rarities;

namespace TestMod.Content.Items.Weapons.Summon
{
    // 心宿二 Antares（移植自灾厄 Mod，解耦版）
    // 说明:
    // - 首次使用: 正常召唤一只 AntaresMinion。
    // - 已存在时再次使用: 不再召唤新的,而是把一个召唤槽塞给现有的 Antares,让它的星座变大、多连几个星点。
    public class Antares : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
        }

        public override void SetDefaults()
        {
            Item.width = 62;
            Item.height = 62;
            Item.damage = 26;
            Item.useAnimation = 25;
            Item.useTime = 25;
            Item.mana = 0;
            Item.knockBack = 10f;
            Item.buffType = ModContent.BuffType<AntaresBuff>();
            Item.shoot = ModContent.ProjectileType<AntaresMinion>();
            Item.DamageType = DamageClass.Summon;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item44;
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.value = Item.buyPrice(gold: 70);
            Item.noMelee = true;
            Item.autoReuse = true;
        }

        public override bool CanUseItem(Player player) => true;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            const string textKey = "Mods.TestMod.Items.Antares.";
            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.owner != Main.myPlayer || projectile.ModProjectile is not AntaresMinion minion)
                    continue;
                string stage = Language.GetTextValue(textKey + "Stage" + minion.AscensionTier);
                tooltips.Add(new TooltipLine(Mod, "CurrentStage", Language.GetTextValue(textKey + "CurrentStage",
                    projectile.minionSlots, stage, minion.SlotDamageMultiplier,
                    Math.Max(1f, projectile.minionSlots / 3f))));
                return;
            }
            tooltips.Add(new TooltipLine(Mod, "CurrentStage", Language.GetTextValue(textKey + "NotSummoned")));
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // 已经有一只 Antares 存在: 不新召唤,改为把一个召唤槽塞给它。
            if (player.ownedProjectileCounts[type] > 0)
            {
                Projectile antares = null;
                foreach (var proj in Main.ActiveProjectiles)
                {
                    if (proj.type == type && proj.owner == player.whoAmI)
                    {
                        antares = proj;
                        break;
                    }
                }
                if (antares != null)
                {
                    antares.ai[1]++;
                    antares.netUpdate = true;
                }
                return false;
            }
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.HallowedBar, 15)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
