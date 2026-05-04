using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Projectiles;

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
            Item.damage = 60;
            Item.knockBack = 3f;
            Item.useTime = 28;
            Item.useAnimation = 28;
            Item.shoot = ModContent.ProjectileType<TestWhipProj>();
            Item.shootSpeed = 3f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item152;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = false;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.buyPrice(gold: 10);
        }

        // 接受近战词缀（锋利/传奇等）
        public override bool MeleePrefix() => true;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Wood, 10)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }
}
