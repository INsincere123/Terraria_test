using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Items.Armor.TerraArmor
{
    public abstract class TerraArmorItem : ModItem
    {
        [CloneByReference]
        private readonly int[] _visualSlots = new int[4];
        protected abstract EquipType ArmorPart { get; }
        protected abstract int VanillaItem(TerraArmorMode mode);
        protected abstract int VanillaSlot(TerraArmorMode mode);
        public override string Texture => $"Terraria/Images/Item_{VanillaItem(TerraArmorMode.Warrior)}";

        public override void Load()
        {
            foreach (TerraArmorMode mode in System.Enum.GetValues<TerraArmorMode>())
            {
                string path = ArmorPart switch
                {
                    EquipType.Head => "Terraria/Images/Armor_Head_",
                    EquipType.Body => "Terraria/Images/Armor/Armor_",
                    _ => "Terraria/Images/Armor_Legs_"
                };
                // 独立穿戴槽仅复用原版图片，不触发原版按盔甲 ID 判断的发光、拖影或属性。
                _visualSlots[(int)mode] = EquipLoader.AddEquipTexture(Mod, path + VanillaSlot(mode), ArmorPart,
                    name: Name + "_" + mode);
            }
        }

        public override void AutoDefaults()
        {
            base.AutoDefaults();
            // Load 早于物品 ID 注册；在实例初始化时再绑定正确的穿戴槽，不能以当时的 Type=0 建映射。
            int slot = EquipLoader.GetEquipSlot(Mod, Name + "_" + TerraArmorMode.Warrior, ArmorPart);
            if (slot < 0) throw new System.InvalidOperationException($"未注册泰拉盔甲穿戴槽：{Name}。");
            switch (ArmorPart)
            {
                case EquipType.Head: Item.headSlot = slot; break;
                case EquipType.Body: Item.bodySlot = slot; break;
                case EquipType.Legs: Item.legSlot = slot; break;
            }
        }

        internal int VisualSlot(TerraArmorMode mode) => _visualSlots[(int)mode];

        internal int MatchVisualSlot(int visibleSlot, TerraArmorMode mode)
        {
            for (TerraArmorMode candidate = TerraArmorMode.Warrior; candidate <= TerraArmorMode.Summoner; candidate++)
                if (visibleSlot == VisualSlot(candidate)) return VisualSlot(mode);
            return visibleSlot; // 保留其他时装及变身的外观覆盖。
        }

        private Texture2D CurrentItemTexture()
        {
            TerraArmorMode mode = Main.LocalPlayer.TryGetModPlayer(out TerraArmorPlayer armor) ? armor.Mode : TerraArmorMode.Warrior;
            int type = VanillaItem(mode);
            Main.instance.LoadItem(type);
            return TextureAssets.Item[type].Value;
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            Texture2D texture = CurrentItemTexture();
            scale *= (float)System.Math.Max(frame.Width, frame.Height) / System.Math.Max(texture.Width, texture.Height);
            spriteBatch.Draw(texture, position, null, Item.GetAlpha(drawColor), 0f, texture.Size() / 2f, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor,
            ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = CurrentItemTexture();
            Vector2 position = Item.position - Main.screenPosition +
                new Vector2(Item.width / 2f, Item.height - texture.Height / 2f);
            spriteBatch.Draw(texture, position, null, Item.GetAlpha(lightColor), rotation, texture.Size() / 2f,
                scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void SetStaticDefaults()
        {
            if (Main.dedServ) return;
            foreach (TerraArmorMode mode in System.Enum.GetValues<TerraArmorMode>())
            {
                int slot = VisualSlot(mode), original = VanillaSlot(mode);
                if (ArmorPart == EquipType.Head)
                {
                    Item.headType[slot] = Type;
                    ArmorIDs.Head.Sets.DrawHead[slot] = ArmorIDs.Head.Sets.DrawHead[original];
                    ArmorIDs.Head.Sets.DrawFullHair[slot] = ArmorIDs.Head.Sets.DrawFullHair[original];
                    ArmorIDs.Head.Sets.DrawHatHair[slot] = ArmorIDs.Head.Sets.DrawHatHair[original];
                }
                else if (ArmorPart == EquipType.Body)
                {
                    Item.bodyType[slot] = Type;
                    ArmorIDs.Body.Sets.HidesArms[slot] = ArmorIDs.Body.Sets.HidesArms[original];
                    ArmorIDs.Body.Sets.HidesHands[slot] = ArmorIDs.Body.Sets.HidesHands[original];
                    ArmorIDs.Body.Sets.HidesTopSkin[slot] = ArmorIDs.Body.Sets.HidesTopSkin[original];
                }
                else
                {
                    Item.legType[slot] = Type;
                    ArmorIDs.Legs.Sets.HidesBottomSkin[slot] = ArmorIDs.Legs.Sets.HidesBottomSkin[original];
                }
            }
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.defense = 50;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Generic) += 0.33f;
            player.GetCritChance(DamageClass.Generic) += 12f;
            player.GetAttackSpeed(DamageClass.Generic) += 0.10f;
            player.GetArmorPenetration(DamageClass.Generic) += 10f;
            player.moveSpeed += 0.11f;
            player.manaCost -= 0.08f;
            player.endurance += 0.03f;
            player.lifeRegen += 10;
            player.maxMinions += 3;
            player.maxTurrets++;
        }

        protected void RegisterRecipe(int solar, int vortex, int nebula, int stardust)
        {
            CreateRecipe().AddIngredient(solar).AddIngredient(vortex).AddIngredient(nebula)
                .AddIngredient(stardust).AddTile(TileID.LunarCraftingStation).Register();
        }
    }
}
