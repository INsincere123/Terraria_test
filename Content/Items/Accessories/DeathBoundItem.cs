using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TestMod.Content.Items.Accessories
{
    /// <summary>
    /// 死亡绑定饰品基类：死亡一次后该物品实例永久失效，重新合成可获得新的有效实例。
    /// 子类在 UpdateAccessory 里调用 base 后检查 IsActive 即可。
    /// </summary>
    public abstract class DeathBoundItem : ModItem
    {
        public bool IsActive = true;

        /// <summary>物品有效时每帧调用，子类在此写实际效果。</summary>
        protected abstract void UpdateEffect(Player player, bool hideVisual);

        /// <summary>死亡失效时显示的消息，返回 null 则不显示。</summary>
        public virtual string BreakMessage => null;

        public sealed override void UpdateAccessory(Player player, bool hideVisual)
        {
            if (!IsActive)
                return;
            UpdateEffect(player, hideVisual);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            if (IsActive)
                return true;

            Asset<Texture2D> texture = TextureAssets.Item[Item.type];
            spriteBatch.Draw(texture.Value, position, frame, Color.Gray, 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            if (!IsActive)
                tooltips.Add(new TooltipLine(Mod, "DeathBound", "已破碎")
                {
                    OverrideColor = Microsoft.Xna.Framework.Color.Gray
                });
        }

        public override void SaveData(TagCompound tag)
        {
            tag["active"] = IsActive;
        }

        public override void LoadData(TagCompound tag)
        {
            IsActive = !tag.ContainsKey("active") || tag.GetBool("active");
        }
    }
}
