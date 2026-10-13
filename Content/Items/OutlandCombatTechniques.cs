using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TestMod.Common.Systems;

namespace TestMod.Content.Items
{
    public abstract class OutlandCombatTechniquesItem : ModItem
    {
        internal abstract byte Volume { get; }
        protected abstract int VanillaBook { get; }
        internal abstract int Price { get; }
        public override string Texture => $"Terraria/Images/Item_{VanillaBook}";

        public override void SetDefaults()
        {
            Item.CloneDefaults(VanillaBook);
            Item.value = Price;
            Item.autoReuse = false;
            Item.useTime = Item.useAnimation;
        }

        public override bool CanUseItem(Player player) => !OutlandCombatTechniquesSystem.IsUsed(Volume);

        // 消耗与永久状态一并由服务器提交，避免两个玩家同时使用时丢失物品。
        public override bool ConsumeItem(Player player) => false;

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                OutlandCombatTechniquesSystem.RequestUse(player, Volume);
            return true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            if (OutlandCombatTechniquesSystem.IsUsed(Volume))
                tooltips.Add(new TooltipLine(Mod, "AlreadyUsed",
                    Language.GetTextValue("Mods.TestMod.OutlandCombatTechniques.AlreadyUsed")));
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            spriteBatch.Draw(TextureAssets.Item[Type].Value, position, frame,
                Item.GetAlpha(drawColor).MultiplyRGB(Color.Gold), 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = TextureAssets.Item[Type].Value;
            Vector2 position = Item.position - Main.screenPosition +
                new Vector2(Item.width / 2f, Item.height - texture.Height / 2f);
            spriteBatch.Draw(texture, position, null, Item.GetAlpha(lightColor).MultiplyRGB(Color.Gold),
                rotation, texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    public sealed class OutlandCombatTechniquesVolumeOne : OutlandCombatTechniquesItem
    {
        internal override byte Volume => 1;
        protected override int VanillaBook => ItemID.CombatBook;
        internal override int Price => Item.buyPrice(gold: 25);
    }

    public sealed class OutlandCombatTechniquesVolumeTwo : OutlandCombatTechniquesItem
    {
        internal override byte Volume => 2;
        protected override int VanillaBook => ItemID.CombatBookVolumeTwo;
        internal override int Price => Item.buyPrice(platinum: 10);
    }
}
