using System;
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
    public abstract class SkywardSigilBuff : ModBuff
    {
        protected abstract int StackSign { get; }

        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override bool RightClick(int buffIndex) => false;

        public override void PostDraw(SpriteBatch spriteBatch, int buffIndex, BuffDrawParams drawParams)
        {
            int net = Main.LocalPlayer.GetModPlayer<SkywardSigilPlayer>().NetStacks;
            if (Math.Sign(net) != StackSign) return;
            int stacks = Math.Abs(net);
            ChatManager.DrawColorCodedStringWithShadow(
                spriteBatch, FontAssets.MouseText.Value, stacks.ToString(),
                drawParams.Position + new Vector2(22f, 20f),
                stacks >= SkywardSigilPlayer.MaxStacks ? Color.Gold : Color.White,
                0f, Vector2.Zero, new Vector2(0.75f));
        }
    }

    public class SkywardOverlookBuff : SkywardSigilBuff
    {
        protected override int StackSign => 1;
    }

    public class SkywardUpwardBuff : SkywardSigilBuff
    {
        protected override int StackSign => -1;
    }
}
