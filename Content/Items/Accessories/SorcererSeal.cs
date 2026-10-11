using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Items.Accessories
{
    /// <summary>
    /// 法师之证：由魔法手铐、魔力再生手环、魔力斗篷、天界徽章、毁灭者徽章合成。
    /// 复用原饰品的功能效果（受伤回蓝、魔力再生、魔力花自动喝药、受伤星星），不叠加其数值加成。
    /// </summary>
    public class SorcererSeal : ModItem
    {
        public override string Texture => $"Terraria/Images/Item_{ItemID.SorcererEmblem}";

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
            player.statManaMax2 += 60;
            player.manaCost -= 0.12f;
            player.GetDamage(DamageClass.Magic) += 0.25f;
            player.GetCritChance(DamageClass.Magic) += 10f;
            player.lifeRegen += 2; // 1 点/秒（原版 lifeRegen 单位为 0.5 点/秒）
            player.statDefense += 2;

            // 魔力再生手环：魔力再生速度提升
            player.manaRegenDelayBonus += 1f;
            player.manaRegenBonus += 25;

            // 魔法手铐：受伤时 1:1 恢复魔力（原版字段自动处理）
            player.magicCuffs = true;
            player.manaMagnet = true; // 天界徽章：扩大魔力星拾取范围。

            // 魔力斗篷：魔力花（自动使用魔力药水）+ 受伤坠落星星
            player.manaFlower = true;
            player.starCloakItem = Item;
            player.starCloakItem_manaCloakOverrideItem = Item;
        }

        public override void AddRecipes() => CreateRecipe()
            .AddIngredient(ItemID.MagicCuffs)
            .AddIngredient(ItemID.ManaRegenerationBand)
            .AddIngredient(ItemID.ManaCloak)
            .AddIngredient(ItemID.CelestialEmblem)
            .AddIngredient(ItemID.DestroyerEmblem)
            .AddTile(TileID.TinkerersWorkbench)
            .Register();
    }
}
