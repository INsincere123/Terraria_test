using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TestMod.Common.Players;

namespace TestMod.Content.Buffs
{
    public class ClassicGrapeBeerBuff : ModBuff
    {
        public const float DamagePenalty = 0.40f;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = false;
            Main.persistentBuff[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // 加算项减去 40 个百分点；不乘最终伤害，也不修改暴击倍率。
            ApplyPenalty(player);
        }

        internal static void ApplyPenalty(Player player)
            => player.GetModPlayer<ClassicGrapeBeerPlayer>().ApplyPenalty();
    }
}
