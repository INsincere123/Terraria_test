using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Accessories
{
    /// <summary>
    /// 战士之证：由烈火手套、血肉指虎、冰冻护盾、蜜蜂斗篷、毁灭者徽章合成。
    /// 复用烈火手套、蜜蜂斗篷和冰冻护盾功能，面板属性使用本物品数值。
    /// </summary>
    public class WarriorSeal : ModItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.WarriorEmblem}";

        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 1;

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(gold: 10);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // 自定义数值
            player.statDefense += 15;
            player.aggro += 600;
            player.GetDamage(DamageClass.Melee) += 0.25f;
            player.GetCritChance(DamageClass.Melee) += 10f;
            player.GetAttackSpeed(DamageClass.Melee) += 0.2f;

            // 蜜蜂斗篷与冰冻护盾免疫击退；条件保护由 WarriorSealPlayer 统一结算。
            player.noKnockback = true;
            player.honeyCombItem = Item;
            player.starCloakItem = Item;
            player.starCloakItem_beeCloakOverrideItem = Item;

            // 烈火手套功能：岩浆石点燃、自动挥舞、击退、武器变大（不含伤害/攻速数值）
            player.magmaStone = true;
            player.autoReuseGlove = true;
            player.kbGlove = true;
            player.meleeScaleGlove = true;

            // 受伤后 5 秒内免伤 +10%
            player.GetModPlayer<WarriorSealPlayer>().Equip();
        }

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.FireGauntlet)
            .AddIngredient(ItemID.FleshKnuckles)
            .AddIngredient(ItemID.FrozenShield)
            .AddIngredient(ItemID.BeeCloak)
            .AddIngredient(ItemID.DestroyerEmblem)
            .AddTile(TileID.TinkerersWorkbench)
            .Register();
    }
}
