using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    /// <summary>
    /// 时停激活中的玩家头上显示图标（仅作显示用）。
    /// </summary>
    public class TimeStoppedBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // 仅作显示，不写状态逻辑
        }
    }
}
