using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Projectiles;
using TestMod.Rarities;

namespace TestMod.Items.Weapons.Summon
{
    /// <summary>
    /// 测试鞭子：+50% 暴击率，+10 固定标记伤害，命中触发爆炸。
    /// </summary>
    public class TestWhip : ModItem
    {
        public override void SetDefaults()
        {
            Item.DamageType = DamageClass.SummonMeleeSpeed;
            Item.damage = 233;
            Item.knockBack = 10f;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.shoot = ModContent.ProjectileType<TestWhipProj>();
            Item.shootSpeed = 3f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item152;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ModContent.RarityType<AntaresRarity>();
            Item.value = Item.buyPrice(platinum: 10);
            // WhipTagRegistry.cs里调整标记效果
        }

        // 接受近战词缀（锋利/传奇等）
        public override bool MeleePrefix() => true;

        public override void AddRecipes()
        {
            Recipe recipe = Recipe.Create(Type);

            recipe.AddIngredient(ItemID.PumpkingMasterTrophy); // 南瓜王
            recipe.AddIngredient(ItemID.BetsyMasterTrophy); // 双足翼龙
            recipe.AddIngredient(ItemID.FairyQueenMasterTrophy); // 光之女皇
            recipe.AddIngredient(ItemID.FragmentStardust, 25);  // 星尘碎片
            recipe.AddIngredient(ItemID.LunarBar, 25);  // 夜明锭

            recipe.AddTile(TileID.LunarCraftingStation);
            recipe.Register();
        }
    }
}
