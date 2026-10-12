using Terraria;
using Terraria.Localization;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Accessories
{
    /// <summary>
    /// 召唤师之证：由矮人项链、甲虫莎草纸、旧日军团四件套之一（学徒围巾/女猎人圆盾/武僧腰带/侍卫护盾）、毁灭者徽章合成。
    /// </summary>
    public class SummonerSeal : ModItem
    {
        public const string AnyOOACombatAccessoryGroup = "TestMod:AnyOOACombatAccessory";

        public override string Texture => $"Terraria/Images/Item_{ItemID.SummonerEmblem}";

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
            player.maxMinions += 2;
            player.maxTurrets += 1;
            player.GetDamage(DamageClass.Summon) += 0.3f;
            player.GetKnockback(DamageClass.Summon).Base += 2f;
            player.whipRangeMultiplier += 0.2f; // 原版为加法，重置后基础值 1

            SummonCritPlayer.Enable(player);
            player.GetModPlayer<SummonCritPlayer>().BonusCritChance += 10f;
        }

        public override void AddRecipes()
        {
            // 四选一：学徒围巾 / 女猎人圆盾 / 武僧腰带 / 侍卫护盾
            RecipeGroup group = new RecipeGroup(() => Language.GetTextValue("Mods.TestMod.RecipeGroups.AnyOOACombatAccessory"),
                ItemID.ApprenticeScarf, ItemID.HuntressBuckler, ItemID.MonkBelt, ItemID.SquireShield);
            RecipeGroup.RegisterGroup(AnyOOACombatAccessoryGroup, group);

            CreateRecipe()
                .AddIngredient(ItemID.PygmyNecklace)
                .AddIngredient(ItemID.PapyrusScarab)
                .AddRecipeGroup(AnyOOACombatAccessoryGroup)
                .AddIngredient(ItemID.DestroyerEmblem)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}
