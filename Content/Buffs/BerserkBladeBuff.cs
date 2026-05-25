using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using TestMod.Common.Players;

namespace TestMod.Content.Buffs
{
    public class BerserkBladeBuff : ModBuff
    {
        // 复用原版 buff 75 的贴图
        public override string Texture => "Terraria/Images/Buff_75";

        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = true; // 不显示倒计时（由 BerserkBladePlayer 管理存活）
            Main.buffNoSave[Type]        = true; // 退出存档不保留
        }

        // 在 buff 图标右下角绘制当前叠层数
        // 1~9 层：白色数字；10 层（满层）：金色数字
        public override void PostDraw(SpriteBatch spriteBatch, int buffIndex, BuffDrawParams drawParams)
        {
            int stacks = Main.LocalPlayer.GetModPlayer<BerserkBladePlayer>().Stacks;
            if (stacks <= 0) return;

            bool maxed  = stacks >= BerserkBladePlayer.MaxStacks;
            Color color = maxed ? Color.Gold : Color.White;

            // 数字位置：图标（32×32）右下角，scale 0.75 时约占 8px
            Vector2 numPos = drawParams.Position + new Vector2(22f, 20f);
            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch,
                FontAssets.MouseText.Value,
                stacks.ToString(),
                numPos,
                color,
                rotation:  0f,
                origin:    Vector2.Zero,
                baseScale: new Vector2(0.75f));
        }
    }
}
