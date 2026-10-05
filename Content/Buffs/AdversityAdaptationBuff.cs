using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using TestMod.Common.Players;

namespace TestMod.Content.Buffs
{
    // 仅展示适应状态；自定义贴图不会赋予铁皮 Buff 的防御加成。
    public class AdversityAdaptationBuff : ModBuff
    {
        // 自定义图标：Content/Buffs/AdversityAdaptationBuff.png（32x32，alpha 透明底）

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
        }

        public override bool RightClick(int buffIndex) => false;

        public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
        {
            var adaptation = Main.LocalPlayer.GetModPlayer<AdversityAmuletPlayer>();
            tip += "\n" + Language.GetTextValue("Mods.TestMod.Items.AdversityAmulet.AdaptationState",
                adaptation.Stacks, AdversityAmuletPlayer.MaxStacks,
                (int)System.MathF.Round(adaptation.AdaptationReduction * 100f),
                adaptation.RemainingSeconds);
        }

        public override void PostDraw(SpriteBatch spriteBatch, int buffIndex, BuffDrawParams drawParams)
        {
            int stacks = Main.LocalPlayer.GetModPlayer<AdversityAmuletPlayer>().Stacks;
            if (stacks <= 0) return;

            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value,
                stacks.ToString(), drawParams.Position + new Vector2(22f, 20f),
                stacks >= AdversityAmuletPlayer.MaxStacks ? Color.Gold : Color.White,
                0f, Vector2.Zero, new Vector2(0.75f));
        }
    }
}
