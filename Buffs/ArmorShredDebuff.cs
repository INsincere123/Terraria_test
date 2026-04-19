using Terraria;
using Terraria.ModLoader;

namespace 武器test.Buffs
{
    /// <summary>
    /// 破甲debuff：每层减少10点护甲，最多10层
    /// 层数存在 MyGlobalNPC 的字典里，本类只作为"在场"标记
    /// 实际减甲逻辑在 MyGlobalNPC.ModifyIncomingHit 里处理
    /// </summary>
    public class ArmorShredDebuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_69"; // 借用金海debuff图标

        public override void SetStaticDefaults()
        {
            Main.debuff[Type]     = true;
            Main.pvpBuff[Type]    = true;
            Main.buffNoSave[Type] = true;
        }
    }
}