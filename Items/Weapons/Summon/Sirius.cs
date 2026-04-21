using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using 武器test.Buffs;
using 武器test.Projectiles.Minions;

namespace 武器test.Items.Weapons.Summon
{
    // 天狼星 Sirius（移植自灾厄 Mod，解耦版）
    // 说明:
    // - 首次使用: 正常召唤一只 SiriusMinion。
    // - 已存在时再次使用: 不再召唤新的,而是把一个召唤槽塞给现有的 Sirius,让它的星座变大、多连几个星点。
    public class Sirius : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
        }

        public override void SetDefaults()
        {
            Item.width = 62;
            Item.height = 62;
            Item.damage = 90;
            Item.useAnimation = 24;
            Item.useTime = 24;
            Item.mana = 10;
            Item.knockBack = 10f;
            Item.buffType = ModContent.BuffType<SiriusBuff>();
            Item.shoot = ModContent.ProjectileType<SiriusMinion>();
            Item.DamageType = DamageClass.Summon;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item44;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.buyPrice(gold: 20);
            Item.noMelee = true;
            Item.autoReuse = true;
        }

        public override bool CanUseItem(Player player) => true;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // 已经有一只 Sirius 存在: 不新召唤,改为把一个召唤槽塞给它。
            if (player.ownedProjectileCounts[type] > 0)
            {
                Projectile sirius = null;
                foreach (var proj in Main.ActiveProjectiles)
                {
                    if (proj.type == type && proj.owner == player.whoAmI)
                    {
                        sirius = proj;
                        break;
                    }
                }
                if (sirius != null)
                {
                    sirius.ai[1]++;
                    sirius.netUpdate = true;
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