using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using 武器test.Buffs;
using 武器test.Projectiles.Minions;

namespace 武器test.Items.Weapons.Summon
{
    /// <summary>
    /// 雷之呼吸·壹式召唤杖
    /// 召唤"雷之剑士"仆从，采用"蓄力 → 同步爆发"型输出结构。
    /// </summary>
    public class ThunderBreathingStaff : ModItem
    {
        public override void SetStaticDefaults()
            => ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;

        public override void SetDefaults()
        {
            Item.width = 50;
            Item.height = 50;
            Item.damage = 38;                          // 基础伤害(可根据游戏进度调整)
            Item.mana = 10;
            Item.useAnimation = Item.useTime = 13;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.knockBack = 7f;
            Item.value = Item.sellPrice(gold: 5);
            Item.rare = ItemRarityID.Orange;             // 橙色稀有度
            Item.UseSound = SoundID.Item44;
            Item.autoReuse = true;
            Item.DamageType = DamageClass.Summon;
            Item.buffType = ModContent.BuffType<ThunderBreathingBuff>();
            Item.shoot = ModContent.ProjectileType<ZenitsuMinion>();
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            // 在鼠标位置召唤(限制最远距离，避免跨屏幕召唤)
            Vector2 spawnPos = Main.MouseWorld;
            float maxDist = 1000f;
            if (Vector2.Distance(spawnPos, player.Center) > maxDist)
                spawnPos = player.Center + (spawnPos - player.Center).SafeNormalize(Vector2.Zero) * maxDist;

            var minion = Projectile.NewProjectileDirect(source, spawnPos, Vector2.Zero,
                type, damage, knockback, player.whoAmI);
            minion.originalDamage = Item.damage;
            return false;
        }

        public override void AddRecipes()
        {
            // 简单配方示例，可根据游戏进度调整
            CreateRecipe()
                .AddIngredient(ItemID.MeteoriteBar, 12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
