using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;
using TestMod.Items.DamageTypes;
using TestMod.Rarities;

namespace TestMod.Common.GlobalItems
{
    public partial class TestGlobalItem : GlobalItem
    {
        private static bool IsGodMode(Player player) =>
            player?.active == true && player.GetModPlayer<GodModePlayer>().GodModeBuff;

        public override void SetDefaults(Item item)
        {
            if (item.type == ItemID.AmethystHook)
                item.shootSpeed = 25f;

            if (item.type == ItemID.StormTigerStaff)
            {
                item.damage = 51;
                item.knockBack = 10;
                item.useTime = 20;
                item.useAnimation = 20;
            }

            if (item.type == ItemID.AbigailsFlower)
            {
                item.damage = 14;
                item.knockBack = 2;
                item.mana = 0;
                item.useTime = 20;
                item.useAnimation = 20;
            }
        }

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (line.Mod != "Terraria")
                return true;

            if (line.Name == "ItemName")
            {
                if (item.rare == ModContent.RarityType<AntaresRarity>())
                {
                    AntaresRarity.Draw(item, line);
                    return false;
                }

                return true;
            }

            if (line.Name == "Damage")
            {
                var sb = Main.spriteBatch;
                float time = Main.GlobalTimeWrappedHourly;
                var r = line.Rotation;
                var o = line.Origin;
                var s = line.BaseScale;
                int x = line.X;
                int y = line.Y;
                string text = line.Text;

                DamageClass damageType = item.DamageType;
                if (damageType == TrueDamageClass.Instance)
                    DamageLineRenderer.DrawTrue(sb, text, x, y, r, o, s, time);
                else if (damageType == DamageClass.Melee || damageType == DamageClass.MeleeNoSpeed)
                    DamageLineRenderer.DrawMelee(sb, text, x, y, r, o, s, time);
                else if (damageType == DamageClass.Ranged)
                    DamageLineRenderer.DrawRanged(sb, text, x, y, r, o, s, time);
                else if (damageType == DamageClass.Magic)
                    DamageLineRenderer.DrawMagic(sb, text, x, y, r, o, s, time);
                else if (damageType == DamageClass.Summon || damageType == DamageClass.SummonMeleeSpeed)
                    DamageLineRenderer.DrawSummon(sb, text, x, y, r, o, s, time);
                else
                    return true;

                return false;
            }

            return true;
        }
    }
}
