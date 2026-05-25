using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    /// <summary>
    /// 时停冷却中的玩家头上显示图标（仅作显示用）。
    /// </summary>
    public class TimeStopCDBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.debuff[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // 仅作显示
        }
    }
}
