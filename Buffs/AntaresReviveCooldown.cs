using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace 武器test.Buffs
{
    // 复活冷却 debuff —— 存在期间无法再次触发复活
    // 持续时间由 AntaresHelmet.ReviveCooldown 控制
    // 护士无法移除此 debuff
    public class AntaresReviveCooldown : ModBuff
    {
        public override void SetStaticDefaults()
        {
            // 标记为 debuff，状态栏显示红色边框
            Main.debuff[Type] = true;

            // 护士无法移除
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;

            // 存档时不保存此状态（重新进入游戏后 CD 重置）
            Main.buffNoSave[Type] = true;
        }
    }
}
