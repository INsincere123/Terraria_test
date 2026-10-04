using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TestMod.Common.Utilities
{
    public static partial class DrawUtils
    {
        /// <summary>原版四帧状态纹理的启用/禁用标记，沿用调用方的 SpriteBatch 状态。</summary>
        public static void DrawInventoryToggle(SpriteBatch spriteBatch, Vector2 position, bool enabled)
        {
            if (Main.dedServ) return;

            Texture2D texture = ModContent.Request<Texture2D>("Terraria/Images/Extra_20", AssetRequestMode.ImmediateLoad).Value;
            Rectangle frame = texture.Frame(1, 4, frameY: enabled ? 1 : 2);
            Vector2 offset = new Vector2(16f, 16f) * Main.inventoryScale;
            spriteBatch.Draw(texture, position + offset, frame, Color.White, 0f,
                frame.Size() * 0.5f, Main.inventoryScale, SpriteEffects.None, 0f);
        }
    }
}
