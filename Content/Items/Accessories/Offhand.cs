using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Content.Items.Accessories.Effects;
using TestMod.Content.Projectiles.Accessories;

namespace TestMod.Content.Items.Accessories
{
    /// <summary>
    /// 副手饰品。
    /// 穿戴后，每次命中敌人时从玩家头顶的炮台发射追加弹幕。
    /// 穿透弹幕有 30 帧全局冷却（per-NPC 冷却 10 帧）；普通命中无冷却限制。
    /// </summary>
    public class Offhand : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            OnHitEffectsPlayer.Activate<SubhandOnHitEffect>(player);
            SubhandOnHitEffect.KeepCannonAlive(player);
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.MagnetSphere, 2);          
            recipe.AddTile(TileID.MythrilAnvil);
            recipe.Register();
        }
    }
}
