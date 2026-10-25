using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TestMod.Content.Buffs
{
    public class SmartScopeActiveBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
        }

    }

    public class SmartScopeCooldownBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_21";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }
    }
}
